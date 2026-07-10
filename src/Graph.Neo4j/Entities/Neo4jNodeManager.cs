// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using System.Reflection;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class Neo4jNodeManager(GraphContext context)
{
    private readonly ILogger<Neo4jNodeManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jNodeManager>()
        ?? NullLogger<Neo4jNodeManager>.Instance;
    private readonly EntityFactory _serializer = new EntityFactory(context.LoggerFactory);
    private readonly ComplexPropertyManager _complexPropertyManager = new(context);

    // Root MATCH used by DeleteNodeAsync's count/business-relationship/delete queries. The
    // node's label(s) aren't known at the call site (delete-by-id on INode, e.g. a DynamicNode
    // with an arbitrary/unregistered label), so this can't be scoped to a specific label up
    // front - a plain, label-agnostic MATCH on Id resolves the candidate node(s) directly.
    // labels(n) here is the cheap per-node function (labels already bound by the MATCH), not the
    // database-wide db.labels() catalog procedure that used to be queried separately - as a
    // pre-existing round trip - before this MATCH could even be built (see #135). This is a
    // compile-time constant (not built from a database query result) specifically so its shape
    // is directly testable without a live Neo4j instance.
    internal const string RootMatchPrelude = $@"
        MATCH (n {{Id: $nodeId}})
        WHERE (
            n.{SerializationBridge.EntityKindPropertyName} = $nodeEntityKind
            OR (
                n.{SerializationBridge.EntityKindPropertyName} IS NULL
                AND any(label IN labels(n) WHERE label IN $registeredNodeLabels)
                AND NOT EXISTS {{
                    MATCH ()-[incomingProperty]->(n)
                    WHERE incomingProperty.{ComplexPropertyStorage.RelationshipMarkerProperty} = true
                }}
            )
        )";

    public async Task<TNode> CreateNodeAsync<TNode>(
        TNode node,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Creating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);
            GraphDataModel.EnsureComplexPropertyDepth(node);

            // Validate property constraints at application level
            ValidateNodeProperties(node);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            // Create the main node
            var nodeId = await CreateMainNodeAsync(entity, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            // Create complex properties (throws on failure)
            await _complexPropertyManager.CreateComplexPropertiesAsync(
                transaction.Transaction, nodeId, entity, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

            return node;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating node of type {NodeType}", typeof(TNode).Name);
            throw new GraphException($"Failed to create node: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateNodeAsync<TNode>(
        TNode node,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebug("Updating node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);
            GraphDataModel.EnsureComplexPropertyDepth(node);

            // Validate property constraints at application level
            ValidateNodeProperties(node);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            // Update the node properties. ComplexPropertyManager matches parents by Neo4j's
            // elementId, not the domain Id, so capture it from the same MATCH.
            var parentElementId = await UpdateMainNodeAsync(node.Id, entity, transaction.Transaction, cancellationToken).ConfigureAwait(false);

            if (parentElementId is null)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for update", node.Id);
                throw new EntityNotFoundException($"Node with ID {node.Id} not found for update");
            }

            // Update complex properties (throws on failure)
            await _complexPropertyManager.UpdateComplexPropertiesAsync(
                transaction.Transaction, parentElementId, entity, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Updated node of type {NodeType} with ID {NodeId}", typeof(TNode).Name, node.Id);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Error updating node {NodeId} of type {NodeType}", node.Id, typeof(TNode).Name);
            throw new GraphException($"Failed to update node: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        GraphTransaction transaction,
        bool cascadeDelete = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger.LogDebug("Deleting node with ID: {NodeId}, cascade: {CascadeDelete}", nodeId, cascadeDelete);

        try
        {
            var registeredNodeLabels = await GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false);
            var rootCount = await GetRootCountAsync(
                nodeId,
                registeredNodeLabels,
                transaction.Transaction,
                cancellationToken).ConfigureAwait(false);
            if (rootCount == 0)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for deletion", nodeId);
                throw new EntityNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            if (rootCount > 1)
            {
                throw new GraphException(
                    $"Cannot delete node {nodeId} because the ID matches {rootCount} graph nodes. " +
                    "DeleteNodeAsync requires the ID to identify exactly one node.");
            }

            if (!cascadeDelete)
            {
                // First check if the node has any business relationships (non-complex properties)
                var checkCypher = $@"
                    {RootMatchPrelude}
                    OPTIONAL MATCH (n)-[r]-()
                    WHERE coalesce(r.{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = false
                    RETURN COUNT(r) AS businessRelationshipCount";

                var checkResult = await transaction.Transaction.RunAsync(checkCypher, new
                {
                    nodeId,
                    nodeEntityKind = SerializationBridge.NodeEntityKind,
                    registeredNodeLabels
                }).ConfigureAwait(false);

                var checkRecord = await GetSingleRecordAsync(checkResult, cancellationToken).ConfigureAwait(false);
                var businessRelationshipCount = checkRecord["businessRelationshipCount"].As<int>();

                if (businessRelationshipCount > 0)
                {
                    throw new GraphException(
                        $"Cannot delete node {nodeId} because it has {businessRelationshipCount} relationship(s). " +
                        "Use cascadeDelete=true to force deletion or delete the relationships first.");
                }
            }

            // Now perform the deletion
            var cypher = $@"
                {RootMatchPrelude}
                OPTIONAL MATCH propertyPath = (n)-[propertyRels*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
                WHERE ALL(rel IN propertyRels WHERE rel.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)
                WITH n, [propertyNode IN collect(DISTINCT propertyNode) WHERE propertyNode IS NOT NULL] AS propertyNodes
                FOREACH (propertyNode IN propertyNodes | DETACH DELETE propertyNode)
                DETACH DELETE n
                RETURN true AS wasDeleted";

            var result = await transaction.Transaction.RunAsync(cypher, new
            {
                nodeId,
                nodeEntityKind = SerializationBridge.NodeEntityKind,
                registeredNodeLabels
            }).ConfigureAwait(false);

            var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger.LogWarning("Node with ID {NodeId} not found for deletion", nodeId);
                throw new EntityNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            _logger.LogInformation("Deleted node with ID {NodeId}", nodeId);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Error deleting node with ID: {NodeId}", nodeId);
            throw new GraphException($"Failed to delete node: {ex.Message}", ex);
        }
    }

    private async Task<int> GetRootCountAsync(
        string nodeId,
        string[] registeredNodeLabels,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            {RootMatchPrelude}
            RETURN COUNT(DISTINCT n) AS rootCount";

        var result = await transaction.RunAsync(cypher, new
        {
            nodeId,
            nodeEntityKind = SerializationBridge.NodeEntityKind,
            registeredNodeLabels
        }).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return record["rootCount"].As<int>();
    }

    private async Task<string[]> GetRegisteredNodeLabelsAsync(CancellationToken cancellationToken)
    {
        var labels = await context.SchemaManager
            .GetSchemaRegistry()
            .GetRegisteredNodeLabelsAsync(cancellationToken).ConfigureAwait(false);

        return labels.ToArray();
    }

    private async Task<string> CreateMainNodeAsync(
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        string cypher;

        // For dynamic nodes, use the actual labels from ActualLabels
        if (entity.ActualType.IsAssignableTo(typeof(Graph.DynamicNode)))
        {
            if (entity.ActualLabels != null && entity.ActualLabels.Count > 0)
            {
                // Labels are Cypher identifiers, not parameter values: they can originate from
                // caller-supplied input (DynamicNode.Labels is set at runtime), so validate and
                // escape each one before interpolation.
                var labels = CypherIdentifier.EscapeLabels(entity.ActualLabels);
                cypher = $"CREATE (n:{labels} $props) RETURN elementId(n) AS nodeId";
            }
            else
            {
                // For dynamic nodes with no labels, create without any labels
                cypher = "CREATE (n $props) RETURN elementId(n) AS nodeId";
            }
        }
        else
        {
            // entity.Label is derived from the compile-time type name or a [Node] attribute for
            // strongly-typed nodes, but is still routed through the same escaping for consistency
            // and defense-in-depth.
            var label = CypherIdentifier.Escape(entity.Label, "node label");
            cypher = $"CREATE (n:{label} $props) RETURN elementId(n) AS nodeId";
        }

        var simpleProperties = SerializationHelpers.SerializeSimpleProperties(entity);
        simpleProperties[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;

        // Add Labels property to be stored in Neo4j
        simpleProperties[nameof(Graph.INode.Labels)] =
            entity.ActualLabels == null || entity.ActualLabels.Count == 0 ? [entity.Label] : entity.ActualLabels;

        var result = await transaction.RunAsync(cypher, new { props = simpleProperties }).ConfigureAwait(false);
        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);

        return record["nodeId"].As<string>()
            ?? throw new GraphException("Failed to create node - no ID returned");
    }

    /// <returns>The updated node's Neo4j elementId, or <see langword="null"/> when no node matched.</returns>
    private async Task<string?> UpdateMainNodeAsync(
        string nodeId,
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        string cypher;
        var simpleProperties = SerializationHelpers.SerializeSimpleProperties(entity);
        simpleProperties[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;

        // Add Labels property to be stored in Neo4j
        simpleProperties[nameof(Graph.INode.Labels)] =
            entity.ActualLabels == null || entity.ActualLabels.Count == 0 ? [entity.Label] : entity.ActualLabels;

        // For dynamic nodes, update both properties and labels
        if (entity.ActualType == typeof(Graph.DynamicNode) && entity.ActualLabels != null && entity.ActualLabels.Count > 0)
        {
            // First, get the current labels to remove them
            var getLabelsCypher = "MATCH (n {Id: $nodeId}) RETURN labels(n) AS currentLabels";
            var getLabelsResult = await transaction.RunAsync(getLabelsCypher, new { nodeId }).ConfigureAwait(false);
            var getLabelsRecord = await GetSingleRecordAsync(getLabelsResult, cancellationToken).ConfigureAwait(false);
            var currentLabels = getLabelsRecord["currentLabels"].As<List<string>>() ?? new List<string>();

            // Labels read back from the database or supplied on the entity are still Cypher
            // identifiers, not parameter values, and must be validated/escaped before
            // interpolation - the current labels may predate this validation (data written by an
            // older version of this library, or by another client) and the new labels can
            // originate from caller-supplied input (DynamicNode.Labels).
            var escapedCurrentLabels = currentLabels.Select(label => CypherIdentifier.Escape(label, "node label")).ToList();

            // Build the REMOVE clause for current labels
            var removeLabelsClause = escapedCurrentLabels.Count > 0
                ? $"REMOVE n:{string.Join(":n:", escapedCurrentLabels)} "
                : "";

            // Build the SET clause for new labels
            var newLabels = CypherIdentifier.EscapeLabels(entity.ActualLabels);
            var setLabelsClause = $"SET n:{newLabels} ";

            cypher = $"MATCH (n {{Id: $nodeId}}) {removeLabelsClause}SET n = $props {setLabelsClause}RETURN elementId(n) AS elementId";
        }
        else
        {
            // For non-dynamic nodes, just update properties
            cypher = "MATCH (n {Id: $nodeId}) SET n = $props RETURN elementId(n) AS elementId";
        }

        var result = await transaction.RunAsync(cypher, new { nodeId, props = simpleProperties }).ConfigureAwait(false);
        var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        return records.Count > 0 ? records[0]["elementId"].As<string>() : null;
    }

    private void ValidateNodeProperties<TNode>(TNode node) where TNode : class, Graph.INode
    {
        // For DynamicNode, validate against existing schemas if any
        if (node is DynamicNode dynamicNode)
        {
            // Labels are Cypher identifiers, not values: reject hostile labels here, before any
            // Cypher is built, rather than relying solely on escaping at the point of interpolation.
            foreach (var dynamicNodeLabel in dynamicNode.Labels)
            {
                CypherIdentifier.Validate(dynamicNodeLabel, "node label");
            }

            ValidateDynamicNodeProperties(dynamicNode);
            return;
        }

        var label = Labels.GetLabelFromType(node.GetType());
        var schema = context.SchemaManager.GetSchemaRegistry().GetNodeSchema(label);

        if (schema == null) return;

        foreach (var (propertyName, propertySchema) in schema.Properties)
        {
            var property = node.GetType().GetProperty(propertyName);
            if (property == null) continue;

            var value = property.GetValue(node);

            // Validate required fields
            if (propertySchema.IsRequired)
            {
                if (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                {
                    throw new GraphException($"Property '{propertyName}' on {label} is required and cannot be null or empty.");
                }
            }

            // Validate custom validation rules
            if (propertySchema.Validation is { } validation && value is not null)
            {
                ValidatePropertyValue(propertyName, value, validation, label);
            }
        }
    }

    private void ValidateDynamicNodeProperties(DynamicNode node)
    {
        // Check each label to see if there's a corresponding schema
        foreach (var label in node.Labels)
        {
            var schema = context.SchemaManager.GetSchemaRegistry().GetNodeSchema(label);
            if (schema == null) continue;

            // Found a schema for this label, validate the dynamic node against it
            var validatedProperties = new HashSet<string>();

            foreach (var (propertyName, propertySchema) in schema.Properties)
            {
                // Use the mapped property name from the schema (from PropertyAttribute.Label)
                var mappedPropertyName = propertySchema.Name;

                // Check if the property exists in the dynamic node's properties
                if (!node.Properties.TryGetValue(mappedPropertyName, out var value))
                {
                    // Property doesn't exist in dynamic node
                    if (propertySchema.IsRequired)
                    {
                        throw new GraphException($"Property '{mappedPropertyName}' on {label} is required but not provided in DynamicNode.");
                    }
                    continue;
                }

                // Mark this property as validated
                validatedProperties.Add(mappedPropertyName);

                // Validate required fields
                if (propertySchema.IsRequired)
                {
                    if (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue)))
                    {
                        throw new GraphException($"Property '{mappedPropertyName}' on {label} is required and cannot be null or empty.");
                    }
                }

                // Validate custom validation rules
                if (propertySchema.Validation is { } validation && value is not null)
                {
                    ValidatePropertyValue(mappedPropertyName, value, validation, label);
                }

                // Validate enum values
                if (value is not null)
                {
                    ValidateEnumValue(mappedPropertyName, value, propertySchema.PropertyInfo.PropertyType, label);
                }
            }

            // Check for extra properties that don't exist in the schema
            foreach (var propertyName in node.Properties.Keys)
            {
                if (!validatedProperties.Contains(propertyName))
                {
                    throw new GraphException($"Property '{propertyName}' on {label} is not defined in the schema and cannot be used.");
                }
            }
        }
    }

    private void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
    {
        // MinValue validation
        if (validation.MinValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MinValue) < 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be greater than or equal to {validation.MinValue}. Current value: {value}");
                }
            }
        }

        // MaxValue validation
        if (validation.MaxValue is not null)
        {
            if (value is IComparable comparable)
            {
                if (comparable.CompareTo(validation.MaxValue) > 0)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be less than or equal to {validation.MaxValue}. Current value: {value}");
                }
            }
        }

        // MinLength validation
        if (validation.MinLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length < validation.MinLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a minimum length of {validation.MinLength}. Current length: {stringValue.Length}");
                }
            }
        }

        // MaxLength validation
        if (validation.MaxLength is not null)
        {
            if (value is string stringValue)
            {
                if (stringValue.Length > validation.MaxLength)
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a maximum length of {validation.MaxLength}. Current length: {stringValue.Length}");
                }
            }
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(validation.Pattern))
        {
            if (value is string stringValue)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(stringValue, validation.Pattern))
                {
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must match the pattern '{validation.Pattern}'. Current value: {stringValue}");
                }
            }
        }
    }

    private void ValidateEnumValue(string propertyName, object value, Type propertyType, string entityLabel)
    {
        // Check if the property type is an enum
        if (propertyType.IsEnum)
        {
            // If the value is a string, try to parse it as the enum
            if (value is string stringValue)
            {
                if (!Enum.TryParse(propertyType, stringValue, ignoreCase: true, out _))
                {
                    var validValues = string.Join(", ", Enum.GetNames(propertyType));
                    throw new GraphException($"Property '{propertyName}' on {entityLabel} must be a valid enum value. Valid values are: {validValues}. Current value: {stringValue}");
                }
            }
            // If the value is not a string, check if it can be converted to the enum
            else if (!Enum.IsDefined(propertyType, value))
            {
                var validValues = string.Join(", ", Enum.GetNames(propertyType));
                throw new GraphException($"Property '{propertyName}' on {entityLabel} must be a valid enum value. Valid values are: {validValues}. Current value: {value}");
            }
        }
    }

    private static async Task<IRecord> GetSingleRecordAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> GetCountAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.CountAsync(cancellationToken).ConfigureAwait(false);
    }
}

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

    public async Task<TNode> CreateNodeAsync<TNode>(
        TNode node,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebugNeo4jNodeManager54(typeof(TNode).Name);

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

            _logger.LogInformationNeo4jNodeManager75(typeof(TNode).Name);

            return node;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jNodeManager85(ex, typeof(TNode).Name);
            throw new GraphException($"Failed to create node: {ex.Message}", ex);
        }
    }

    internal async Task<bool> UpdateByElementIdAsync(
        Graph.INode node,
        string elementId,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();

        GraphDataModel.EnsureNoReferenceCycle(node);
        GraphDataModel.EnsureComplexPropertyDepth(node);
        ValidateNodeProperties(node);
        var entity = _serializer.Serialize(node);
        var updated = await UpdateMainNodeByElementIdAsync(
            elementId,
            entity,
            transaction.Transaction,
            cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return false;
        }

        await _complexPropertyManager.UpdateElementBoundComplexPropertiesAsync(
            transaction.Transaction,
            elementId,
            entity,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static async Task<int> DeleteByElementIdsAsync(
        IReadOnlyList<string> elementIds,
        bool cascadeDelete,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(elementIds);
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        if (elementIds.Count == 0)
        {
            return 0;
        }

        if (!cascadeDelete)
        {
            var preflight = $"""
                MATCH (target)
                WHERE elementId(target) IN $targetIds
                OPTIONAL MATCH (target)-[relationship]-()
                WHERE coalesce(relationship.{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = false
                RETURN count(DISTINCT relationship) AS relationshipCount
                """;
            var cursor = await transaction.Transaction.RunAsync(
                preflight,
                new { targetIds = elementIds }).WaitAsync(cancellationToken).ConfigureAwait(false);
            var relationshipCount = (await cursor.SingleAsync(cancellationToken).ConfigureAwait(false))["relationshipCount"]
                .As<long>();
            if (relationshipCount > 0)
            {
                throw new GraphException(
                    $"Cannot delete the selected nodes because they have {relationshipCount} incident user relationship(s). " +
                    "Delete those relationships first or use cascade delete.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var delete = $"""
            MATCH (target)
            WHERE elementId(target) IN $targetIds
            OPTIONAL MATCH propertyPath = (target)-[propertyRelationships*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE ALL(relationship IN propertyRelationships WHERE relationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)
            WITH target, [node IN collect(DISTINCT propertyNode) WHERE node IS NOT NULL] AS propertyNodes
            FOREACH (propertyNode IN propertyNodes | DETACH DELETE propertyNode)
            DETACH DELETE target
            RETURN count(*) AS affectedCount
            """;
        var result = await transaction.Transaction.RunAsync(
            delete,
            new { targetIds = elementIds }).WaitAsync(cancellationToken).ConfigureAwait(false);
        return (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["affectedCount"].As<int>();
    }

    private static async Task<string> CreateMainNodeAsync(
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

        var simpleProperties = BuildElementBoundNodeProperties(entity);

        var result = await transaction.RunAsync(cypher, new { props = simpleProperties }).ConfigureAwait(false);
        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);

        return record["nodeId"].As<string>()
            ?? throw new GraphException("Failed to create node - no ID returned");
    }

    private static async Task<bool> UpdateMainNodeByElementIdAsync(
        string elementId,
        EntityInfo entity,
        IAsyncTransaction transaction,
        CancellationToken cancellationToken)
    {
        var simpleProperties = BuildElementBoundNodeProperties(entity);

        string cypher;
        if (entity.ActualType == typeof(Graph.DynamicNode) && entity.ActualLabels.Count > 0)
        {
            var labelsResult = await transaction.RunAsync(
                "MATCH (n) WHERE elementId(n) = $elementId RETURN labels(n) AS currentLabels",
                new { elementId }).WaitAsync(cancellationToken).ConfigureAwait(false);
            var labelsRecord = await labelsResult.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (labelsRecord is null)
            {
                return false;
            }

            var currentLabels = labelsRecord["currentLabels"].As<List<string>>() ?? [];
            var escapedCurrentLabels = currentLabels
                .Select(label => CypherIdentifier.Escape(label, "node label"))
                .ToArray();
            var removeLabelsClause = escapedCurrentLabels.Length > 0
                ? $"REMOVE n:{string.Join(":", escapedCurrentLabels)} "
                : string.Empty;
            var newLabels = CypherIdentifier.EscapeLabels(entity.ActualLabels);
            cypher = $"MATCH (n) WHERE elementId(n) = $elementId {removeLabelsClause}SET n = $props SET n:{newLabels} RETURN count(n) AS affectedCount";
        }
        else
        {
            cypher = "MATCH (n) WHERE elementId(n) = $elementId SET n = $props RETURN count(n) AS affectedCount";
        }

        var result = await transaction.RunAsync(
            cypher,
            new { elementId, props = simpleProperties }).WaitAsync(cancellationToken).ConfigureAwait(false);
        var record = await result.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return record is not null && record["affectedCount"].As<int>() == 1;
    }

    internal static Dictionary<string, object?> BuildElementBoundNodeProperties(EntityInfo entity)
    {
        return SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(pair => pair.Key != SimpleCollectionStorageCodec.GetPayloadPropertyName(nameof(Graph.INode.Labels)) &&
                pair.Key != SimpleCollectionStorageCodec.GetNullIndexesPropertyName(nameof(Graph.INode.Labels)) &&
                pair.Key != SimpleCollectionStorageCodec.GetElementTypePropertyName(nameof(Graph.INode.Labels)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    internal void ValidateNodeProperties<TNode>(TNode node) where TNode : class, Graph.INode
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

    private static void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
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

    private static void ValidateEnumValue(string propertyName, object value, Type propertyType, string entityLabel)
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

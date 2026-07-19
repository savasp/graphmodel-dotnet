// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using System.Reflection;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


internal sealed class AgeNodeManager(AgeGraphContext context)
{
    private readonly ILogger<AgeNodeManager> _logger = context.LoggerFactory?.CreateLogger<AgeNodeManager>()
        ?? NullLogger<AgeNodeManager>.Instance;
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
    // is directly testable without a live Age instance.
    internal const string RootMatchPrelude = $@"
        MATCH (n {{Id: $nodeId}})
        WHERE n.{SerializationBridge.EntityKindPropertyName} = $nodeEntityKind";

    public async Task<TNode> CreateNodeAsync<TNode>(
        TNode node,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebugAgeNodeManager44(typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);
            GraphDataModel.EnsureComplexPropertyDepth(node);

            // Validate property constraints at application level
            ValidateNodeProperties(node);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            await ValidateNodeUniquenessAsync(node, transaction.Runner, excludeId: null, cancellationToken)
                .ConfigureAwait(false);

            if (await NodeExistsAsync(node.Id, entity.Label, transaction.Runner, cancellationToken).ConfigureAwait(false))
            {
                throw new GraphException($"Node with ID '{node.Id}' already exists.");
            }

            // Create the main node
            var nodeId = await CreateMainNodeAsync(entity, transaction.Runner, cancellationToken).ConfigureAwait(false);

            // Create complex properties (throws on failure)
            await _complexPropertyManager.CreateComplexPropertiesAsync(
                transaction.Runner, nodeId, entity, cancellationToken).ConfigureAwait(false);

            _logger.LogInformationAgeNodeManager73(typeof(TNode).Name, node.Id);

            return node;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeNodeManager79(ex, typeof(TNode).Name);
            throw new GraphException("Failed to create node.", ex);
        }
    }

    /// <summary>
    /// Creates <paramref name="node"/> only when no node with the same Id already exists,
    /// implementing MERGE-by-Id endpoint semantics for the subgraph create: an existing endpoint is
    /// reused as-is (its properties and complex-property subtree are left untouched).
    /// </summary>
    public async Task CreateNodeIfMissingAsync<TNode>(
        TNode node,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        if (await NodeExistsByIdAsync(node.Id, transaction.Runner, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebugAgeNodeManager44(typeof(TNode).Name, node.Id);
            return;
        }

        await CreateNodeAsync(node, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateNodeAsync<TNode>(
        TNode node,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TNode : class, Graph.INode
    {
        ArgumentNullException.ThrowIfNull(node);

        _logger.LogDebugAgeNodeManager92(typeof(TNode).Name, node.Id);

        try
        {
            // Validate no reference cycles
            GraphDataModel.EnsureNoReferenceCycle(node);
            GraphDataModel.EnsureComplexPropertyDepth(node);

            // Validate property constraints at application level
            ValidateNodeProperties(node);

            await ValidateNodeUniquenessAsync(node, transaction.Runner, node.Id, cancellationToken)
                .ConfigureAwait(false);

            // Serialize the node
            var entity = _serializer.Serialize(node);

            // Update the node properties. ComplexPropertyManager matches parents by Age's
            // elementId, not the domain Id, so capture it from the same MATCH.
            var parentElementId = await UpdateMainNodeAsync(node.Id, entity, transaction.Runner, cancellationToken).ConfigureAwait(false);

            if (parentElementId is null)
            {
                _logger.LogWarningAgeNodeManager115(node.Id);
                throw new EntityNotFoundException($"Node with ID {node.Id} not found for update");
            }

            // Update complex properties (throws on failure)
            await _complexPropertyManager.UpdateComplexPropertiesAsync(
                transaction.Runner, parentElementId, entity, cancellationToken).ConfigureAwait(false);

            _logger.LogInformationAgeNodeManager123(typeof(TNode).Name, node.Id);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeNodeManager128(ex, node.Id, typeof(TNode).Name);
            throw new GraphException("Failed to update node.", ex);
        }
    }

    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        AgeGraphTransaction transaction,
        bool cascadeDelete = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        _logger.LogDebugAgeNodeManager141(nodeId, cascadeDelete);

        try
        {
            var rootCount = await GetRootCountAsync(
                nodeId,
                transaction.Runner,
                cancellationToken).ConfigureAwait(false);
            if (rootCount == 0)
            {
                _logger.LogWarningAgeNodeManager151(nodeId);
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

                var checkResult = await transaction.Runner.RunAsync(checkCypher, new
                {
                    nodeId,
                    nodeEntityKind = SerializationBridge.NodeEntityKind,
                }, cancellationToken).ConfigureAwait(false);

                var checkRecord = await GetSingleRecordAsync(checkResult, cancellationToken).ConfigureAwait(false);
                var businessRelationshipCount = checkRecord["businessRelationshipCount"].As<int>();

                if (businessRelationshipCount > 0)
                {
                    throw new GraphException(
                        $"Cannot delete node {nodeId} because it has {businessRelationshipCount} relationship(s). " +
                        "Use cascadeDelete=true to force deletion or delete the relationships first.");
                }
            }

            // AGE 1.7 lacks ALL(...); the index-based comprehension keeps Neo4j's semantics: every
            // relationship in the path must be a complex-property relationship.
            var findPropertyNodes = $@"
                {RootMatchPrelude}
                MATCH (n)-[propertyRels*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
                WHERE size([age_hop IN range(0, size(propertyRels) - 1) WHERE propertyRels[toInteger(age_hop)].{ComplexPropertyStorage.RelationshipMarkerProperty} = true]) = size(propertyRels)
                RETURN DISTINCT id(propertyNode) AS propertyNodeId";
            var propertyResult = await transaction.Runner.RunAsync(findPropertyNodes, new
            {
                nodeId,
                nodeEntityKind = SerializationBridge.NodeEntityKind,
            }, cancellationToken).ConfigureAwait(false);
            var propertyNodes = await propertyResult.ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var propertyNode in propertyNodes)
            {
                await transaction.Runner.RunAsync(
                    "MATCH (propertyNode) WHERE id(propertyNode) = toInteger($propertyNodeId) DETACH DELETE propertyNode RETURN true AS deleted",
                    new { propertyNodeId = propertyNode["propertyNodeId"].As<string>() }, cancellationToken).ConfigureAwait(false);
            }

            var cypher = $@"
                {RootMatchPrelude}
                DETACH DELETE n
                RETURN true AS wasDeleted";

            var result = await transaction.Runner.RunAsync(cypher, new
            {
                nodeId,
                nodeEntityKind = SerializationBridge.NodeEntityKind,
            }, cancellationToken).ConfigureAwait(false);

            var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
            var wasDeleted = record["wasDeleted"].As<bool>();

            if (!wasDeleted)
            {
                _logger.LogWarningAgeNodeManager224(nodeId);
                throw new EntityNotFoundException($"Node with ID {nodeId} not found for deletion");
            }

            _logger.LogInformationAgeNodeManager228(nodeId);
            return true;
        }
        catch (Exception ex) when (ex is not GraphException and not OperationCanceledException)
        {
            _logger.LogErrorAgeNodeManager233(ex, nodeId);
            throw new GraphException("Failed to delete node.", ex);
        }
    }

    private static async Task<int> GetRootCountAsync(
        string nodeId,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            {RootMatchPrelude}
            RETURN COUNT(DISTINCT n) AS rootCount";

        var result = await transaction.RunAsync(cypher, new
        {
            nodeId,
            nodeEntityKind = SerializationBridge.NodeEntityKind,
        }, cancellationToken).ConfigureAwait(false);

        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);
        return record["rootCount"].As<int>();
    }

    private static async Task<string> CreateMainNodeAsync(
        EntityInfo entity,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var simpleProperties = BuildNodeProperties(entity);
        var parameters = new Dictionary<string, object?>();
        var setClause = AgeCypherProperties.BuildSetClause("n", simpleProperties, parameters, "nodeProperty");
        var cypher = $"CREATE (n:{SerializationBridge.PhysicalNodeLabel}) {setClause} RETURN id(n) AS nodeId";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);
        var record = await GetSingleRecordAsync(result, cancellationToken).ConfigureAwait(false);

        return record["nodeId"].As<string>()
            ?? throw new GraphException("Failed to create node - no ID returned");
    }

    private static async Task<bool> NodeExistsAsync(
        string id,
        string label,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var result = await transaction.RunAsync(
            "MATCH (n {Id: $nodeId}) WHERE $nodeLabel IN coalesce(n.inheritance_labels, []) RETURN count(n) AS existingCount",
            new { nodeId = id, nodeLabel = label }, cancellationToken).ConfigureAwait(false);
        return (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["existingCount"].As<long>() > 0;
    }

    private static async Task<bool> NodeExistsByIdAsync(
        string id,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var result = await transaction.RunAsync(
            "MATCH (n {Id: $nodeId}) RETURN count(n) AS existingCount",
            new { nodeId = id }, cancellationToken).ConfigureAwait(false);
        return (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["existingCount"].As<long>() > 0;
    }

    /// <returns>The updated node's Age elementId, or <see langword="null"/> when no node matched.</returns>
    private static async Task<string?> UpdateMainNodeAsync(
        string nodeId,
        EntityInfo entity,
        AgeQueryRunner transaction,
        CancellationToken cancellationToken)
    {
        var simpleProperties = BuildNodeProperties(entity);
        var parameters = new Dictionary<string, object?> { ["nodeId"] = nodeId };
        var setClause = AgeCypherProperties.BuildSetClause("n", simpleProperties, parameters, "nodeProperty");
        var cypher = $"MATCH (n {{Id: $nodeId}}) {setClause} RETURN id(n) AS elementId";

        var result = await transaction.RunAsync(cypher, parameters, cancellationToken).ConfigureAwait(false);
        var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (records.Count > 1)
        {
            throw new GraphException(
                $"Cannot update node {nodeId} because the ID matches {records.Count} graph nodes. " +
                "UpdateNodeAsync requires the ID to identify exactly one node.");
        }

        return records.Count > 0 ? records[0]["elementId"].As<string>() : null;
    }

    internal static Dictionary<string, object?> BuildNodeProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity);
        properties[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;
        properties["inheritance_labels"] = GetInheritanceLabels(entity);
        return properties;
    }

    private static IReadOnlyList<string> GetInheritanceLabels(EntityInfo entity)
    {
        if (entity.ActualType == typeof(Graph.DynamicNode))
        {
            return entity.ActualLabels;
        }

        var labels = new List<string>();
        for (var type = entity.ActualType; type is not null && typeof(Graph.INode).IsAssignableFrom(type); type = type.BaseType)
        {
            if (type == typeof(Graph.Node) || type == typeof(object))
            {
                break;
            }

            labels.Add(Labels.GetLabelFromType(type));
        }

        return labels.Distinct(StringComparer.Ordinal).ToArray();
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
            if (propertySchema.IsRequired &&
                (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))))
            {
                throw new GraphException($"Property '{propertyName}' on {label} is required and cannot be null or empty.");
            }

            // Validate custom validation rules
            if (propertySchema.Validation is { } validation && value is not null)
            {
                ValidatePropertyValue(propertyName, value, validation, label);
            }
        }
    }

    private async Task ValidateNodeUniquenessAsync<TNode>(
        TNode node,
        AgeQueryRunner transaction,
        string? excludeId,
        CancellationToken cancellationToken)
        where TNode : class, Graph.INode
    {
        var checks = BuildNodeUniquenessChecks(node, excludeId);

        // Take every lock before the first probe: holding them through the write that follows makes
        // probe-then-write atomic against a competing transaction claiming the same values.
        await transaction.AcquireUniquenessLocksAsync(
            checks.Select(check => check.LockKey).ToArray(),
            cancellationToken).ConfigureAwait(false);

        foreach (var check in checks)
        {
            var result = await transaction.RunAsync(
                check.Cypher,
                check.Parameters,
                cancellationToken).ConfigureAwait(false);
            var count = (await result.SingleAsync(cancellationToken).ConfigureAwait(false))["duplicateCount"].As<long>();
            if (count > 0)
            {
                throw new GraphException(check.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Builds the uniqueness probes for <paramref name="node"/>. A <see cref="DynamicNode"/> is
    /// checked against the registered schema of every label it carries, reading values from its
    /// property bag by mapped storage name - the same resolution
    /// <see cref="ValidateDynamicNodeProperties"/> uses for required/validation rules, so a dynamic
    /// write cannot bypass constraints a typed write of the same label would honour.
    /// </summary>
    internal IReadOnlyList<AgeUniquenessCheck> BuildNodeUniquenessChecks<TNode>(
        TNode node,
        string? excludeId)
        where TNode : class, Graph.INode
    {
        if (node is DynamicNode dynamicNode)
        {
            return dynamicNode.Labels
                .Select(dynamicLabel => context.SchemaManager.GetSchemaRegistry().GetNodeSchema(dynamicLabel))
                .OfType<EntitySchemaInfo>()
                .DistinctBy(schema => schema.Label, StringComparer.OrdinalIgnoreCase)
                .SelectMany(schema => BuildChecksForSchema(
                    schema,
                    property => dynamicNode.Properties.GetValueOrDefault(property.Name),
                    excludeId))
                .ToArray();
        }

        var label = Labels.GetLabelFromType(node.GetType());
        var schema = context.SchemaManager.GetSchemaRegistry().GetNodeSchema(label);
        return schema is null
            ? []
            : BuildChecksForSchema(schema, property => property.PropertyInfo.GetValue(node), excludeId);
    }

    private List<AgeUniquenessCheck> BuildChecksForSchema(
        EntitySchemaInfo schema,
        Func<PropertySchemaInfo, object?> readValue,
        string? excludeId)
    {
        var label = schema.Label;
        var checks = new List<AgeUniquenessCheck>();
        var keyProperties = schema.GetKeyProperties().ToArray();
        if (keyProperties.Length > 0)
        {
            checks.Add(BuildCheck(keyProperties, "composite key"));
        }

        foreach (var property in schema.Properties.Values.Where(property => property.IsUnique && !property.IsKey))
        {
            checks.Add(BuildCheck([property], $"unique property '{property.Name}'"));
        }

        return checks;

        AgeUniquenessCheck BuildCheck(
            IReadOnlyList<PropertySchemaInfo> properties,
            string constraint)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["entityLabel"] = label,
                ["excludeId"] = excludeId,
            };
            var predicates = new List<string>
            {
                "size([age_label IN coalesce(n.inheritance_labels, []) " +
                    "WHERE toLower(age_label) = toLower($entityLabel)]) > 0",
            };
            if (excludeId is not null)
            {
                predicates.Add("n.Id <> $excludeId");
            }

            var values = new List<object?>(properties.Count);
            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var parameterName = $"uniqueValue{index}";
                var value = SerializationBridge.ToAgeValue(readValue(property));
                parameters[parameterName] = value;
                values.Add(value);
                predicates.Add($"n.{CypherIdentifier.Escape(property.Name, "property name")} = ${parameterName}");
            }

            var constraintKey = AgeUniquenessCheck.BuildConstraintKey(label, constraint, values);
            return new AgeUniquenessCheck(
                $"MATCH (n) WHERE {string.Join(" AND ", predicates)} RETURN count(n) AS duplicateCount",
                parameters,
                $"Node '{label}' violates {constraint} uniqueness.",
                constraintKey,
                AgeUniquenessLockKey.Compute(
                    context.GraphName,
                    AgeUniquenessLockKey.NodeEntityKind,
                    label,
                    constraint,
                    values));
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
                if (propertySchema.IsRequired &&
                    (value == null || (value is string stringValue && string.IsNullOrWhiteSpace(stringValue))))
                {
                    throw new GraphException($"Property '{mappedPropertyName}' on {label} is required and cannot be null or empty.");
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
            var extraProperty = node.Properties.Keys.FirstOrDefault(propertyName => !validatedProperties.Contains(propertyName));
            if (extraProperty is not null)
            {
                throw new GraphException($"Property '{extraProperty}' on {label} is not defined in the schema and cannot be used.");
            }
        }
    }

    private static void ValidatePropertyValue(string propertyName, object value, PropertyValidation validation, string entityLabel)
    {
        // MinValue validation
        if (validation.MinValue is not null &&
            value is IComparable minComparable &&
            minComparable.CompareTo(validation.MinValue) < 0)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must be greater than or equal to {validation.MinValue}. Current value: {value}");
        }

        // MaxValue validation
        if (validation.MaxValue is not null &&
            value is IComparable maxComparable &&
            maxComparable.CompareTo(validation.MaxValue) > 0)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must be less than or equal to {validation.MaxValue}. Current value: {value}");
        }

        // MinLength validation
        if (validation.MinLength is not null &&
            value is string minLengthValue &&
            minLengthValue.Length < validation.MinLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a minimum length of {validation.MinLength}. Current length: {minLengthValue.Length}");
        }

        // MaxLength validation
        if (validation.MaxLength is not null &&
            value is string maxLengthValue &&
            maxLengthValue.Length > validation.MaxLength)
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must have a maximum length of {validation.MaxLength}. Current length: {maxLengthValue.Length}");
        }

        // Pattern validation
        if (!string.IsNullOrEmpty(validation.Pattern) &&
            value is string patternValue &&
            !System.Text.RegularExpressions.Regex.IsMatch(patternValue, validation.Pattern))
        {
            throw new GraphException($"Property '{propertyName}' on {entityLabel} must match the pattern '{validation.Pattern}'. Current value: {patternValue}");
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

    private static async Task<AgeRecord> GetSingleRecordAsync(AgeResultCursor result, CancellationToken cancellationToken)
    {
        return await result.SingleAsync(cancellationToken).ConfigureAwait(false);
    }
}

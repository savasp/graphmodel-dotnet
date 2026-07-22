// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Handles the creation and management of complex properties as separate nodes in the graph.
/// </summary>
internal sealed class ComplexPropertyManager(GraphContext context)
{
    private readonly ILogger<ComplexPropertyManager> logger = context.LoggerFactory?.CreateLogger<ComplexPropertyManager>()
        ?? NullLogger<ComplexPropertyManager>.Instance;

    /// <summary>
    /// A value node awaiting creation: the serialized entity plus the relationship type and
    /// parameter maps needed to create it under its parent.
    /// </summary>
    private sealed record PendingValueNode(
        string ParentElementId,
        string RelationshipType,
        EntityInfo Entity,
        Dictionary<string, object?> NodeProperties,
        Dictionary<string, object> RelationshipProperties);

    /// <summary>
    /// A value node to be created inside a single composed statement, addressed by in-statement
    /// Cypher variables (parent and its own) rather than a database elementId.
    /// </summary>
    /// <param name="ParentVariable">The Cypher variable bound to the parent node.</param>
    /// <param name="Variable">The Cypher variable to bind this value node to.</param>
    /// <param name="RelationshipType">The (unescaped) marker relationship type linking parent and child.</param>
    /// <param name="Label">The (unescaped) label of the value node.</param>
    /// <param name="NodeProperties">The value node's simple-property map.</param>
    /// <param name="RelationshipProperties">The marker relationship's property map.</param>
    internal sealed record ValueNodeSpec(
        string ParentVariable,
        string Variable,
        string RelationshipType,
        string Label,
        Dictionary<string, object?> NodeProperties,
        Dictionary<string, object> RelationshipProperties);

    /// <summary>
    /// Cypher clauses and parameters that replace selected complex properties below every row
    /// currently bound to the supplied root variable.
    /// </summary>
    internal sealed record ElementBoundMutationFragment(
        string Cypher,
        IReadOnlyDictionary<string, object?> Parameters);

    /// <summary>
    /// Walks the complex-property subtree of <paramref name="rootEntity"/> breadth-first and returns
    /// a flat list of value nodes to create, each addressed by an in-statement Cypher variable
    /// derived from <paramref name="rootVariable"/>. This shares the property/relationship shaping
    /// used by the per-node create path, so both paths persist complex properties identically, but
    /// binds nodes by variable so the whole subtree can be composed into a single statement.
    /// </summary>
    internal static IReadOnlyList<ValueNodeSpec> CollectValueNodeSpecs(
        string rootVariable,
        EntityInfo rootEntity)
    {
        var specs = new List<ValueNodeSpec>();
        var queue = new Queue<(string Variable, EntityInfo Entity, int Depth)>();
        queue.Enqueue((rootVariable, rootEntity, 0));
        var counter = 0;

        while (queue.Count > 0)
        {
            var (parentVariable, entity, depth) = queue.Dequeue();
            if (entity.ComplexProperties.Count == 0)
                continue;

            if (depth >= GraphDataModel.DefaultDepthAllowed &&
                entity.ComplexProperties.Values.Any(property => property.Value is not null))
            {
                throw new GraphException(
                    $"Complex properties cannot exceed {GraphDataModel.DefaultDepthAllowed} levels of depth.");
            }

            foreach (var (propertyName, complexProperty) in entity.ComplexProperties)
            {
                switch (complexProperty.Value)
                {
                    case EntityInfo childEntity:
                        specs.Add(BuildValueNodeSpec(
                            parentVariable, rootVariable, ref counter, propertyName, complexProperty, childEntity,
                            sequenceNumber: 0, queue, depth));
                        break;

                    case EntityCollection collection:
                        var index = 0;
                        foreach (var item in collection.Entities)
                        {
                            if (item is not null)
                            {
                                specs.Add(BuildValueNodeSpec(
                                    parentVariable, rootVariable, ref counter, propertyName, complexProperty, item,
                                    index, queue, depth));
                            }

                            index++;
                        }
                        break;

                    case null:
                        continue;

                    default:
                        throw new GraphException(
                            $"Unsupported complex property type: {complexProperty.Value.GetType().Name} for property {propertyName}");
                }
            }
        }

        return specs;
    }

    /// <summary>
    /// Builds one statement-local replacement fragment. The caller supplies the frozen-root match
    /// and scalar setters, then appends this fragment and its final return clause. All old subtree
    /// deletion and new value-node creation therefore succeeds or fails as one Neo4j statement.
    /// </summary>
    internal static ElementBoundMutationFragment BuildElementBoundMutationFragment(
        string rootVariable,
        EntityInfo replacementEntity,
        IReadOnlyList<string> relationshipTypesToClear,
        IReadOnlyList<string> propertyNamesToClear,
        IReadOnlyList<string> rootScalarPropertiesToClear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootVariable);
        ArgumentNullException.ThrowIfNull(replacementEntity);
        ArgumentNullException.ThrowIfNull(relationshipTypesToClear);
        ArgumentNullException.ThrowIfNull(propertyNamesToClear);
        ArgumentNullException.ThrowIfNull(rootScalarPropertiesToClear);
        if (relationshipTypesToClear.Count == 0)
        {
            return new ElementBoundMutationFragment(string.Empty, new Dictionary<string, object?>());
        }

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["__complexRelationshipTypes"] = relationshipTypesToClear,
        };
        var cypher = new System.Text.StringBuilder();
        // Force Neo4j to serialize competing replacements before either transaction reads the old subtree.
        // The reserved lock property is removed in the same statement and never reaches committed storage.
        cypher.AppendLine(FormattableString.Invariant(
            $"SET {rootVariable}.__graphModelComplexMutationLock = true"));
        cypher.AppendLine(FormattableString.Invariant(
            $"REMOVE {rootVariable}.__graphModelComplexMutationLock"));
        cypher.AppendLine(FormattableString.Invariant($"WITH {rootVariable}"));
        cypher.AppendLine(FormattableString.Invariant($"OPTIONAL MATCH ({rootVariable})-[__complexOwnerRelationship]->(__complexPropertyRoot)"));
        cypher.AppendLine(FormattableString.Invariant(
            $"WHERE __complexOwnerRelationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true AND type(__complexOwnerRelationship) IN $__complexRelationshipTypes"));
        cypher.AppendLine(FormattableString.Invariant(
            $"OPTIONAL MATCH (__complexPropertyRoot)-[__complexPropertyRelationships*0..{GraphDataModel.DefaultDepthAllowed - 1}]->(__complexPropertyNode)"));
        cypher.AppendLine(FormattableString.Invariant(
            $"WHERE ALL(relationship IN __complexPropertyRelationships WHERE relationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)"));
        cypher.AppendLine(FormattableString.Invariant(
            $"WITH {rootVariable}, [node IN collect(DISTINCT __complexPropertyNode) WHERE node IS NOT NULL] AS __complexPropertyNodes"));
        cypher.AppendLine(
            "FOREACH (__complexPropertyNode IN __complexPropertyNodes | " +
            "DETACH DELETE __complexPropertyNode)");

        if (rootScalarPropertiesToClear.Count > 0)
        {
            var properties = rootScalarPropertiesToClear.Select(propertyName =>
                $"{rootVariable}.{CypherIdentifier.Escape(propertyName, "dynamic property name")}");
            cypher.AppendLine(FormattableString.Invariant($"REMOVE {string.Join(", ", properties)}"));
        }

        var companionPropertiesToClear = propertyNamesToClear
            .SelectMany(ComplexCollectionStorageCodec.GetCompanionPropertyNames)
            .Select(propertyName =>
                $"{rootVariable}.{CypherIdentifier.Escape(propertyName, "complex-collection companion name")}")
            .ToArray();
        if (companionPropertiesToClear.Length > 0)
        {
            cypher.AppendLine(FormattableString.Invariant(
                $"REMOVE {string.Join(", ", companionPropertiesToClear)}"));
        }

        var collectionMetadata = ComplexCollectionStorageCodec.EncodeProperties(
            replacementEntity.ComplexProperties,
            static value => value);
        if (collectionMetadata.Count > 0)
        {
            parameters["__complexCollectionMetadata"] = collectionMetadata;
            cypher.AppendLine(FormattableString.Invariant(
                $"SET {rootVariable} += $__complexCollectionMetadata"));
        }

        var specs = CollectValueNodeSpecs(rootVariable, replacementEntity);
        for (var index = 0; index < specs.Count; index++)
        {
            var spec = specs[index];
            var relationshipVariable = $"__complexRelationship{index}";
            var nodeParameter = $"__complexNodeProperties{index}";
            var relationshipParameter = $"__complexRelationshipProperties{index}";
            parameters[nodeParameter] = spec.NodeProperties;
            parameters[relationshipParameter] = spec.RelationshipProperties;

            cypher.AppendLine(FormattableString.Invariant(
                $"CREATE ({spec.ParentVariable})-[{relationshipVariable}:{CypherIdentifier.Escape(spec.RelationshipType, "complex-property relationship type")}]->({spec.Variable}:{CypherIdentifier.Escape(spec.Label, "complex-property node label")})"));
            cypher.AppendLine(FormattableString.Invariant(
                $"SET {relationshipVariable} = ${relationshipParameter}, {spec.Variable} = ${nodeParameter}"));
        }

        return new ElementBoundMutationFragment(cypher.ToString(), parameters);
    }

    private static ValueNodeSpec BuildValueNodeSpec(
        string parentVariable,
        string rootVariable,
        ref int counter,
        string propertyName,
        Property property,
        EntityInfo entity,
        int sequenceNumber,
        Queue<(string Variable, EntityInfo Entity, int Depth)> queue,
        int depth)
    {
        var (relationshipType, nodeProps, relProps) = BuildValueNodeContent(
            propertyName,
            property,
            entity,
            sequenceNumber);
        var variable = $"{rootVariable}_v{counter++}";
        queue.Enqueue((variable, entity, depth + 1));
        return new ValueNodeSpec(parentVariable, variable, relationshipType, entity.Label, nodeProps, relProps);
    }

    public async Task CreateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default) =>
        await CreateComplexPropertiesCoreAsync(transaction, parentId, entity, cancellationToken).ConfigureAwait(false);

    private async Task CreateComplexPropertiesCoreAsync(
        IAsyncTransaction transaction,
        string parentElementId,
        EntityInfo entity,
        CancellationToken cancellationToken)
    {
        // Breadth-first: create all value nodes of one nesting level with a single UNWIND
        // statement per (relationship type, label) group, instead of one statement per node.
        var currentLevel = new List<(string ParentElementId, EntityInfo Entity)> { (parentElementId, entity) };

        for (var depth = 0; currentLevel.Count > 0; depth++)
        {
            var pending = CollectPendingValueNodes(currentLevel, depth);
            currentLevel = await CreateLevelAsync(transaction, pending, cancellationToken).ConfigureAwait(false);
        }
    }

    private List<PendingValueNode> CollectPendingValueNodes(
        List<(string ParentElementId, EntityInfo Entity)> level,
        int depth)
    {
        var pending = new List<PendingValueNode>();

        foreach (var (parentElementId, entity) in level)
        {
            if (entity.ComplexProperties.Count == 0)
                continue;

            if (depth >= GraphDataModel.DefaultDepthAllowed &&
                entity.ComplexProperties.Values.Any(property => property.Value is not null))
            {
                throw new GraphException(
                    $"Complex properties cannot exceed {GraphDataModel.DefaultDepthAllowed} levels of depth.");
            }

            foreach (var (propertyName, complexProperty) in entity.ComplexProperties)
            {
                switch (complexProperty.Value)
                {
                    case EntityInfo childEntity:
                        pending.Add(CreatePendingValueNode(
                            parentElementId, propertyName, complexProperty, childEntity,
                            sequenceNumber: 0));
                        break;

                    case EntityCollection collection:
                        var index = 0;
                        foreach (var item in collection.Entities)
                        {
                            if (item is not null)
                            {
                                pending.Add(CreatePendingValueNode(
                                    parentElementId, propertyName, complexProperty, item,
                                    index));
                            }

                            index++;
                        }
                        break;

                    case null:
                        logger.LogDebugComplexPropertyManager88(propertyName);
                        continue;

                    default:
                        logger.LogWarningComplexPropertyManager92(complexProperty.Value.GetType().Name, propertyName);
                        throw new GraphException(
                            $"Unsupported complex property type: {complexProperty.Value.GetType().Name} for property {propertyName}");
                }
            }
        }

        return pending;
    }

    private static PendingValueNode CreatePendingValueNode(
        string parentElementId,
        string propertyName,
        Property property,
        EntityInfo entity,
        int sequenceNumber)
    {
        var (relationshipType, nodeProps, relProps) = BuildValueNodeContent(
            propertyName,
            property,
            entity,
            sequenceNumber);
        return new PendingValueNode(parentElementId, relationshipType, entity, nodeProps, relProps);
    }

    /// <summary>
    /// Builds the marker relationship type and the node/relationship property maps for a single
    /// complex-property value node. Shared by the per-node create path (which addresses parents by
    /// database elementId) and the single-statement subgraph path (which addresses them by Cypher
    /// variable), so both persist complex properties identically.
    /// </summary>
    private static (string RelationshipType, Dictionary<string, object?> NodeProperties, Dictionary<string, object> RelationshipProperties)
        BuildValueNodeContent(
            string propertyName,
            Property property,
            EntityInfo entity,
            int sequenceNumber)
    {
        var relationshipType = property.RelationshipType ?? (property.PropertyInfo is null
            ? GraphDataModel.PropertyNameToRelationshipTypeName(propertyName)
            : GraphDataModel.GetComplexPropertyRelationshipType(property.PropertyInfo));

        var nodeProps = SerializationHelpers.SerializeSimpleProperties(entity);
        var relProps = new Dictionary<string, object>
        {
            ["SequenceNumber"] = sequenceNumber,
            [ComplexPropertyStorage.RelationshipMarkerProperty] = true
        };
        return (relationshipType, nodeProps, relProps);
    }

    /// <summary>
    /// Creates every pending value node of one nesting level, one UNWIND statement per
    /// (relationship type, label) group, and returns the created nodes paired with their entities
    /// so the next level can be created under them.
    /// </summary>
    private async Task<List<(string ParentElementId, EntityInfo Entity)>> CreateLevelAsync(
        IAsyncTransaction transaction,
        List<PendingValueNode> pending,
        CancellationToken cancellationToken)
    {
        var nextLevel = new List<(string ParentElementId, EntityInfo Entity)>(pending.Count);

        // Relationship types and labels are Cypher identifiers, so they cannot be parameterized:
        // rows can only share a statement when both match.
        foreach (var group in pending.GroupBy(node => (node.RelationshipType, node.Entity.Label)))
        {
            var escapedRelationshipType = CypherIdentifier.Escape(
                group.Key.RelationshipType, "complex-property relationship type");
            var escapedLabel = CypherIdentifier.Escape(group.Key.Label, "complex-property node label");

            var groupNodes = group.ToList();
            var rows = groupNodes.Select((node, rowId) => new Dictionary<string, object>
            {
                ["rowId"] = rowId,
                ["parentId"] = node.ParentElementId,
                ["props"] = node.NodeProperties,
                ["relProps"] = node.RelationshipProperties
            }).ToList();

            var cypher = @$"
                UNWIND $rows AS row
                MATCH (parent)
                WHERE elementId(parent) = row.parentId
                CREATE (parent)-[r:{escapedRelationshipType}]->(complex:{escapedLabel})
                SET r = row.relProps, complex = row.props
                RETURN row.rowId AS rowId, elementId(complex) AS nodeId";

            var result = await transaction.RunAsync(cypher, new { rows }).ConfigureAwait(false);
            var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);

            if (records.Count != groupNodes.Count)
            {
                throw new GraphException("Failed to create complex property node");
            }

            foreach (var record in records)
            {
                var rowId = record["rowId"].As<int>();
                var complexNodeId = record["nodeId"].As<string>()
                    ?? throw new GraphException("Failed to create complex property node");
                nextLevel.Add((complexNodeId, groupNodes[rowId].Entity));
            }

            logger.LogDebugComplexPropertyManager182(groupNodes.Count, group.Key.RelationshipType, group.Key.Label);
        }

        return nextLevel;
    }

    public async Task UpdateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        // First, delete all existing complex property relationships
        await DeleteExistingComplexPropertiesAsync(transaction, parentId, cancellationToken).ConfigureAwait(false);

        // Then create the new ones
        await CreateComplexPropertiesAsync(transaction, parentId, entity, cancellationToken).ConfigureAwait(false);
    }

    internal async Task UpdateElementBoundComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentElementId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        await DeleteExistingComplexPropertiesAsync(
            transaction,
            parentElementId,
            cancellationToken).ConfigureAwait(false);
        await CreateComplexPropertiesAsync(transaction, parentElementId, entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteExistingComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        CancellationToken cancellationToken)
    {
        var cypher = $@"
            MATCH (parent)
            WHERE elementId(parent) = $parentId
            OPTIONAL MATCH (parent)-[propertyRels*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE ALL(rel IN propertyRels WHERE rel.{ComplexPropertyStorage.RelationshipMarkerProperty} = true)
            WITH [node IN collect(DISTINCT propertyNode) WHERE node IS NOT NULL] AS propertyNodes
            FOREACH (propertyNode IN propertyNodes | DETACH DELETE propertyNode)
            RETURN size(propertyNodes) AS deletedCount";

        var result = await transaction.RunAsync(cypher, new { parentId }).ConfigureAwait(false);

        var deletedCount = (await GetFirstRecordAsync(result, cancellationToken).ConfigureAwait(false))["deletedCount"].As<int>();

        logger.LogDebugComplexPropertyManager221(deletedCount, parentId);
    }

    private static async Task<IRecord> GetFirstRecordAsync(IResultCursor result, CancellationToken cancellationToken)
    {
        return await result.FirstAsync(cancellationToken).ConfigureAwait(false);
    }
}

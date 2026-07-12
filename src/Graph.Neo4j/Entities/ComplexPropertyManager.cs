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

    public async Task CreateComplexPropertiesAsync(
        IAsyncTransaction transaction,
        string parentId,
        EntityInfo entity,
        CancellationToken cancellationToken = default)
    {
        // Breadth-first: create all value nodes of one nesting level with a single UNWIND
        // statement per (relationship type, label) group, instead of one statement per node.
        var currentLevel = new List<(string ParentElementId, EntityInfo Entity)> { (parentId, entity) };

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
                            parentElementId, propertyName, complexProperty, childEntity, sequenceNumber: 0));
                        break;

                    case EntityCollection collection:
                        var index = 0;
                        foreach (var item in collection.Entities)
                        {
                            pending.Add(CreatePendingValueNode(
                                parentElementId, propertyName, complexProperty, item, index++));
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
        var relationshipType = property.RelationshipType ?? (property.PropertyInfo is null
            ? GraphDataModel.PropertyNameToRelationshipTypeName(propertyName)
            : GraphDataModel.GetComplexPropertyRelationshipType(property.PropertyInfo));

        var nodeProps = SerializationHelpers.SerializeSimpleProperties(entity);
        nodeProps[nameof(Graph.IEntity.Id)] = Guid.NewGuid().ToString("D");
        nodeProps[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;
        var relProps = new Dictionary<string, object>
        {
            [nameof(Graph.IEntity.Id)] = Guid.NewGuid().ToString("D"),
            ["SequenceNumber"] = sequenceNumber,
            [ComplexPropertyStorage.RelationshipMarkerProperty] = true
        };

        return new PendingValueNode(parentElementId, relationshipType, entity, nodeProps, relProps);
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

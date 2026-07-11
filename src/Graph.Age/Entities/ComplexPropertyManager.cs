// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Handles the creation and management of complex properties as separate nodes in the graph.
/// </summary>
internal sealed class ComplexPropertyManager(AgeGraphContext context)
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
        AgeQueryRunner transaction,
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
                        logger.LogDebug("Skipping null complex property {PropertyName}", propertyName);
                        continue;

                    default:
                        logger.LogWarning(
                            "Unsupported complex property type: {PropertyType} for property {PropertyName}",
                            complexProperty.Value.GetType().Name,
                            propertyName);
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
        nodeProps["inheritance_labels"] = entity.ActualLabels.Count > 0
            ? entity.ActualLabels
            : [entity.Label];
        var relProps = new Dictionary<string, object>
        {
            [nameof(Graph.IEntity.Id)] = Guid.NewGuid().ToString("D"),
            ["SequenceNumber"] = sequenceNumber,
            [ComplexPropertyStorage.RelationshipMarkerProperty] = true,
            ["inheritance_labels"] = new[] { relationshipType },
        };

        return new PendingValueNode(parentElementId, relationshipType, entity, nodeProps, relProps);
    }

    /// <summary>
    /// Creates every pending value node of one nesting level, one UNWIND statement per
    /// (relationship type, label) group, and returns the created nodes paired with their entities
    /// so the next level can be created under them.
    /// </summary>
    private async Task<List<(string ParentElementId, EntityInfo Entity)>> CreateLevelAsync(
        AgeQueryRunner transaction,
        List<PendingValueNode> pending,
        CancellationToken cancellationToken)
    {
        var nextLevel = new List<(string ParentElementId, EntityInfo Entity)>(pending.Count);

        foreach (var node in pending)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["parentId"] = node.ParentElementId,
            };
            var nodeSet = AgeCypherProperties.BuildSetClause("complex", node.NodeProperties, parameters, "nodeProperty");
            var relationshipSet = AgeCypherProperties.BuildSetClause(
                "r",
                node.RelationshipProperties.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal),
                parameters,
                "relationshipProperty");

            var cypher = @$"
                MATCH (parent)
                WHERE id(parent) = toInteger($parentId)
                CREATE (parent)-[r:{SerializationBridge.PhysicalRelationshipType}]->(complex:{SerializationBridge.PhysicalNodeLabel})
                {relationshipSet}
                {nodeSet}
                RETURN id(complex) AS nodeId";

            var result = await transaction.RunAsync(cypher, parameters).ConfigureAwait(false);
            var record = await result.SingleAsync(cancellationToken).ConfigureAwait(false);
            var complexNodeId = record["nodeId"].As<string>()
                ?? throw new GraphException("Failed to create complex property node");
            nextLevel.Add((complexNodeId, node.Entity));

            logger.LogDebug(
                "Created complex property node of type {RelationshipType} to {Label}",
                node.RelationshipType, node.Entity.Label);
        }

        return nextLevel;
    }

    public async Task UpdateComplexPropertiesAsync(
        AgeQueryRunner transaction,
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
        AgeQueryRunner transaction,
        string parentId,
        CancellationToken cancellationToken)
    {
        var findCypher = $@"
            MATCH (parent)
            WHERE id(parent) = toInteger($parentId)
            MATCH (parent)-[propertyRels*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE coalesce(propertyRels[toInteger(0)].{ComplexPropertyStorage.RelationshipMarkerProperty}, false) = true
            RETURN DISTINCT id(propertyNode) AS propertyNodeId";

        var result = await transaction.RunAsync(findCypher, new { parentId }).ConfigureAwait(false);
        var propertyNodes = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var propertyNode in propertyNodes)
        {
            await transaction.RunAsync(
                "MATCH (propertyNode) WHERE id(propertyNode) = toInteger($propertyNodeId) DETACH DELETE propertyNode RETURN true AS deleted",
                new { propertyNodeId = propertyNode["propertyNodeId"].As<string>() }).ConfigureAwait(false);
        }

        logger.LogDebug(
            "Deleted {DeletedCount} complex property relationships for parent {ParentId}",
            propertyNodes.Count, parentId);
    }

    private static async Task<AgeRecord> GetFirstRecordAsync(AgeResultCursor result, CancellationToken cancellationToken)
    {
        return await result.FirstAsync(cancellationToken).ConfigureAwait(false);
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;

/// <summary>
/// Creates an endpoint–relationship–endpoint subgraph through one Npgsql batch execution. All
/// domain IDs, including complex-property value-node IDs, are assigned before execution so later
/// commands never depend on client-side reads from earlier result sets.
/// </summary>
internal sealed class AgeSubgraphManager(AgeGraphContext context)
{
    private const string TransientCreatedMarkerPrefix = "__graphModelSubgraphCreated";

    private readonly ComplexPropertyManager complexPropertyManager = new(context);

    public async Task CreateSubgraphAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        bool createMissingEndpoints,
        AgeGraphTransaction transaction,
        CancellationToken cancellationToken)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        var sourceEntity = SerializeNode(source);
        var targetEntity = SerializeNode(target);
        var relationshipEntity = SerializeRelationship(relationship);
        var sourceChecks = context.NodeManager.BuildNodeUniquenessChecks(source, excludeId: null);
        var targetChecks = context.NodeManager.BuildNodeUniquenessChecks(target, excludeId: null);
        var relationshipChecks = context.RelationshipManager.BuildRelationshipUniquenessChecks(
            relationship,
            excludeId: null);

        var marker = $"{TransientCreatedMarkerPrefix}_{Guid.NewGuid():N}";
        var sameEndpoint = string.Equals(source.Id, target.Id, StringComparison.Ordinal);
        if (!createMissingEndpoints && sameEndpoint)
        {
            throw new GraphException("Create-only subgraph endpoints must have distinct IDs.");
        }

        var valueNodeLevels = createMissingEndpoints && sameEndpoint
            ? complexPropertyManager.PlanSubgraphValueNodes((source.Id, sourceEntity))
            : complexPropertyManager.PlanSubgraphValueNodes(
                (source.Id, sourceEntity),
                (target.Id, targetEntity));
        var commands = BuildCommands(
            source,
            relationship,
            target,
            sourceEntity,
            targetEntity,
            relationshipEntity,
            sourceChecks,
            targetChecks,
            relationshipChecks,
            valueNodeLevels,
            marker,
            createMissingEndpoints,
            sameEndpoint);

        var results = await transaction.Runner.RunBatchAsync(commands, cancellationToken).ConfigureAwait(false);
        ValidateResults(
            results,
            source,
            relationship,
            target,
            sourceChecks,
            targetChecks,
            relationshipChecks,
            valueNodeLevels,
            createMissingEndpoints,
            sameEndpoint);
    }

    private EntityInfo SerializeNode<TNode>(TNode node)
        where TNode : class, Graph.INode
    {
        GraphDataModel.EnsureNoReferenceCycle(node);
        GraphDataModel.EnsureComplexPropertyDepth(node);
        context.NodeManager.ValidateNodeProperties(node);
        return context.EntityFactory.Serialize(node);
    }

    private EntityInfo SerializeRelationship<TRelationship>(TRelationship relationship)
        where TRelationship : class, Graph.IRelationship
    {
        GraphDataModel.EnsureNoReferenceCycle(relationship);
        GraphDataModel.EnsureComplexPropertyDepth(relationship);
        context.RelationshipManager.ValidateRelationshipProperties(relationship);

        var entity = context.EntityFactory.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException(
                $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
        }

        return entity;
    }

    private static List<AgeBatchCommand> BuildCommands<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        EntityInfo sourceEntity,
        EntityInfo targetEntity,
        EntityInfo relationshipEntity,
        IReadOnlyList<AgeUniquenessCheck> sourceChecks,
        IReadOnlyList<AgeUniquenessCheck> targetChecks,
        IReadOnlyList<AgeUniquenessCheck> relationshipChecks,
        IReadOnlyList<IReadOnlyList<ComplexPropertyManager.SubgraphValueNode>> valueNodeLevels,
        string marker,
        bool createMissingEndpoints,
        bool sameEndpoint)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        var commands = new List<AgeBatchCommand>
        {
            CountCommand(
                "source_exists",
                "MATCH (n {Id: $id}) RETURN count(n) AS existingCount",
                new Dictionary<string, object?> { ["id"] = source.Id },
                "existingCount"),
            CountCommand(
                "target_exists",
                "MATCH (n {Id: $id}) RETURN count(n) AS existingCount",
                new Dictionary<string, object?> { ["id"] = target.Id },
                "existingCount"),
            CountCommand(
                "relationship_exists",
                "MATCH ()-[r {Id: $id}]-() RETURN count(r) AS existingCount",
                new Dictionary<string, object?> { ["id"] = relationship.Id },
                "existingCount"),
        };

        AddUniquenessCommands(commands, "source_unique", sourceChecks);
        AddUniquenessCommands(commands, "target_unique", targetChecks);
        AddUniquenessCommands(commands, "relationship_unique", relationshipChecks);

        if (createMissingEndpoints)
        {
            commands.Add(BuildMergeRootCommand("source_root", source.Id, sourceEntity, marker));
            if (!sameEndpoint)
            {
                commands.Add(BuildMergeRootCommand("target_root", target.Id, targetEntity, marker));
            }
        }
        else
        {
            commands.Add(BuildCreateOnlyRootsCommand(sourceEntity, targetEntity, source.Id, target.Id, marker));
        }

        for (var depth = 0; depth < valueNodeLevels.Count; depth++)
        {
            commands.Add(BuildValueNodeLevelCommand(depth, valueNodeLevels[depth], marker));
        }

        commands.Add(BuildRelationshipCommand(
            relationship,
            relationshipEntity,
            marker,
            createMissingEndpoints));
        commands.Add(CountCommand(
            "cleanup",
            $"""
            MATCH (n:{SerializationBridge.PhysicalNodeLabel})
            WHERE n.{marker} = true
            REMOVE n.{marker}
            RETURN count(n) AS cleanedCount
            """,
            new Dictionary<string, object?>(),
            "cleanedCount"));

        return commands;
    }

    private static void AddUniquenessCommands(
        List<AgeBatchCommand> commands,
        string namePrefix,
        IReadOnlyList<AgeUniquenessCheck> checks)
    {
        for (var index = 0; index < checks.Count; index++)
        {
            commands.Add(CountCommand(
                $"{namePrefix}_{index}",
                checks[index].Cypher,
                checks[index].Parameters,
                "duplicateCount"));
        }
    }

    private static AgeBatchCommand BuildCreateOnlyRootsCommand(
        EntityInfo sourceEntity,
        EntityInfo targetEntity,
        string sourceId,
        string targetId,
        string marker)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["sourceId"] = sourceId,
            ["targetId"] = targetId,
        };
        var sourceProperties = AgeNodeManager.BuildNodeProperties(sourceEntity);
        sourceProperties[marker] = true;
        var targetProperties = AgeNodeManager.BuildNodeProperties(targetEntity);
        targetProperties[marker] = true;
        var sourceSet = AgeCypherProperties.BuildSetClause(
            "source",
            sourceProperties,
            parameters,
            "sourceProperty");
        var targetSet = AgeCypherProperties.BuildSetClause(
            "target",
            targetProperties,
            parameters,
            "targetProperty");

        return CountCommand(
            "roots",
            $$"""
            OPTIONAL MATCH (existingSource {Id: $sourceId})
            WITH count(existingSource) AS sourceCount
            OPTIONAL MATCH (existingTarget {Id: $targetId})
            WITH sourceCount, count(existingTarget) AS targetCount
            UNWIND CASE WHEN sourceCount = 0 AND targetCount = 0 THEN [1] ELSE [] END AS createRow
            CREATE (source:{{SerializationBridge.PhysicalNodeLabel}})
            {{sourceSet}}
            CREATE (target:{{SerializationBridge.PhysicalNodeLabel}})
            {{targetSet}}
            RETURN count(source) + count(target) AS createdCount
            """,
            parameters,
            "createdCount");
    }

    private static AgeBatchCommand BuildMergeRootCommand(
        string name,
        string id,
        EntityInfo entity,
        string marker)
    {
        var parameters = new Dictionary<string, object?> { ["id"] = id };
        var properties = AgeNodeManager.BuildNodeProperties(entity);
        properties[marker] = true;
        var setClause = AgeCypherProperties.BuildSetClause(
            "node",
            properties,
            parameters,
            "nodeProperty");

        return CountCommand(
            name,
            $$"""
            OPTIONAL MATCH (existing {Id: $id})
            WITH count(existing) AS existingCount
            UNWIND CASE WHEN existingCount = 0 THEN [1] ELSE [] END AS createRow
            CREATE (node:{{SerializationBridge.PhysicalNodeLabel}})
            {{setClause}}
            RETURN count(node) AS createdCount
            """,
            parameters,
            "createdCount");
    }

    private static AgeBatchCommand BuildValueNodeLevelCommand(
        int depth,
        IReadOnlyList<ComplexPropertyManager.SubgraphValueNode> level,
        string marker)
    {
        var nodePropertyNames = level
            .SelectMany(node => node.NodeProperties.Keys)
            .Append(marker)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var relationshipPropertyNames = level
            .SelectMany(node => node.RelationshipProperties.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var rows = level.Select(node =>
        {
            var nodeProperties = new Dictionary<string, object?>(node.NodeProperties, StringComparer.Ordinal)
            {
                [marker] = true,
            };
            return new Dictionary<string, object?>
            {
                ["rootId"] = node.RootId,
                ["parentId"] = node.ParentId,
                ["nodeProperties"] = ComplexPropertyManager.ExpandProperties(nodeProperties, nodePropertyNames),
                ["relationshipProperties"] = ComplexPropertyManager.ExpandProperties(
                    node.RelationshipProperties,
                    relationshipPropertyNames),
            };
        }).ToList();
        var nodeSet = ComplexPropertyManager.BuildSetClauseFromRowMap(
            "complex",
            "row.nodeProperties",
            nodePropertyNames);
        var relationshipSet = ComplexPropertyManager.BuildSetClauseFromRowMap(
            "propertyRelationship",
            "row.relationshipProperties",
            relationshipPropertyNames);

        return CountCommand(
            $"complex_level_{depth}",
            $"""
            UNWIND $rows AS row
            MATCH (root:{SerializationBridge.PhysicalNodeLabel})
            WHERE root.Id = row.rootId AND root.{marker} = true
            MATCH (parent:{SerializationBridge.PhysicalNodeLabel})
            WHERE parent.Id = row.parentId AND parent.{marker} = true
            CREATE (parent)-[propertyRelationship:{SerializationBridge.PhysicalRelationshipType}]->(complex:{SerializationBridge.PhysicalNodeLabel})
            {relationshipSet}
            {nodeSet}
            RETURN count(complex) AS createdCount
            """,
            new Dictionary<string, object?> { ["rows"] = rows },
            "createdCount");
    }

    private static AgeBatchCommand BuildRelationshipCommand<TRelationship>(
        TRelationship relationship,
        EntityInfo entity,
        string marker,
        bool createMissingEndpoints)
        where TRelationship : class, Graph.IRelationship
    {
        var (sourceNodeId, targetNodeId) = relationship.Direction switch
        {
            RelationshipDirection.Outgoing => (relationship.StartNodeId, relationship.EndNodeId),
            RelationshipDirection.Incoming => (relationship.EndNodeId, relationship.StartNodeId),
            _ => throw new GraphException($"Unsupported relationship direction '{relationship.Direction}'."),
        };
        var parameters = new Dictionary<string, object?>
        {
            ["relationshipId"] = relationship.Id,
            ["sourceNodeId"] = sourceNodeId,
            ["targetNodeId"] = targetNodeId,
        };
        var properties = AgeRelationshipManager.BuildRelationshipProperties(entity);
        var setClause = AgeCypherProperties.BuildSetClause(
            "relationship",
            properties,
            parameters,
            "relationshipProperty");

        // In create-only mode both endpoints must carry this batch's transient marker: the roots
        // command creates either both endpoints or neither, so the gate keeps the edge from
        // attaching to pre-existing nodes. Merge mode attaches to matched endpoints by design.
        var createdEndpointsGate = createMissingEndpoints
            ? string.Empty
            : $"""

              WITH source, target
              WHERE source.{marker} = true AND target.{marker} = true
              """;

        return CountCommand(
            "relationship",
            $$"""
            OPTIONAL MATCH ()-[existing {Id: $relationshipId}]-()
            WITH count(existing) AS existingCount
            UNWIND CASE WHEN existingCount = 0 THEN [1] ELSE [] END AS createRow
            MATCH (source:{{SerializationBridge.PhysicalNodeLabel}})
            WHERE source.Id = $sourceNodeId
            MATCH (target:{{SerializationBridge.PhysicalNodeLabel}})
            WHERE target.Id = $targetNodeId{{createdEndpointsGate}}
            CREATE (source)-[relationship:{{SerializationBridge.PhysicalRelationshipType}}]->(target)
            {{setClause}}
            RETURN count(relationship) AS createdCount
            """,
            parameters,
            "createdCount");
    }

    private static AgeBatchCommand CountCommand(
        string name,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters,
        string column) => new(name, cypher, parameters, [column]);

    private static void ValidateResults<TSource, TRelationship, TTarget>(
        IReadOnlyList<AgeBatchResult> results,
        TSource source,
        TRelationship relationship,
        TTarget target,
        IReadOnlyList<AgeUniquenessCheck> sourceChecks,
        IReadOnlyList<AgeUniquenessCheck> targetChecks,
        IReadOnlyList<AgeUniquenessCheck> relationshipChecks,
        IReadOnlyList<IReadOnlyList<ComplexPropertyManager.SubgraphValueNode>> valueNodeLevels,
        bool createMissingEndpoints,
        bool sameEndpoint)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        var resultMap = results.ToDictionary(result => result.Name, StringComparer.Ordinal);
        var sourceExists = GetCount(resultMap, "source_exists", "existingCount") > 0;
        var targetExists = GetCount(resultMap, "target_exists", "existingCount") > 0;
        var relationshipExists = GetCount(resultMap, "relationship_exists", "existingCount") > 0;

        ValidateUniqueness(resultMap, "source_unique", sourceChecks, ignore: createMissingEndpoints && sourceExists);
        if (!createMissingEndpoints && sourceExists)
        {
            throw new GraphException($"Node with ID '{source.Id}' already exists.");
        }

        ValidateUniqueness(
            resultMap,
            "target_unique",
            targetChecks,
            ignore: createMissingEndpoints && (targetExists || sameEndpoint));
        if (!createMissingEndpoints && targetExists)
        {
            throw new GraphException($"Node with ID '{target.Id}' already exists.");
        }

        if (!sourceExists && !targetExists && !sameEndpoint)
        {
            var conflictingCheck = sourceChecks.FirstOrDefault(sourceCheck =>
                targetChecks.Any(targetCheck => targetCheck.ConstraintKey == sourceCheck.ConstraintKey));
            if (conflictingCheck is not null)
            {
                throw new GraphException(conflictingCheck.ErrorMessage);
            }
        }

        ValidateUniqueness(resultMap, "relationship_unique", relationshipChecks, ignore: false);
        if (relationshipExists)
        {
            throw new GraphException($"Relationship with ID '{relationship.Id}' already exists.");
        }

        var expectedRootCount = createMissingEndpoints && sameEndpoint
            ? sourceExists ? 0 : 1
            : (sourceExists ? 0 : 1) + (targetExists ? 0 : 1);
        var actualRootCount = createMissingEndpoints
            ? GetCount(resultMap, "source_root", "createdCount") +
                (sameEndpoint ? 0 : GetCount(resultMap, "target_root", "createdCount"))
            : GetCount(resultMap, "roots", "createdCount");
        if (actualRootCount != expectedRootCount)
        {
            throw new GraphException("Failed to create the expected subgraph endpoint nodes.");
        }

        var expectedValueNodeCount = 0L;
        for (var depth = 0; depth < valueNodeLevels.Count; depth++)
        {
            var expectedAtDepth = valueNodeLevels[depth].LongCount(node =>
                node.RootId == source.Id ? !sourceExists : !targetExists);
            var actualAtDepth = GetCount(resultMap, $"complex_level_{depth}", "createdCount");
            if (actualAtDepth != expectedAtDepth)
            {
                throw new GraphException("Failed to create the expected complex-property subtree.");
            }

            expectedValueNodeCount += expectedAtDepth;
        }

        if (GetCount(resultMap, "relationship", "createdCount") != 1)
        {
            throw new GraphException(
                $"Failed to create relationship of type {typeof(TRelationship).Name} from {relationship.StartNodeId} to {relationship.EndNodeId}. " +
                "One or both nodes may not exist.");
        }

        if (GetCount(resultMap, "cleanup", "cleanedCount") != expectedRootCount + expectedValueNodeCount)
        {
            throw new GraphException("Failed to remove transient subgraph creation markers.");
        }
    }

    private static void ValidateUniqueness(
        IReadOnlyDictionary<string, AgeBatchResult> results,
        string namePrefix,
        IReadOnlyList<AgeUniquenessCheck> checks,
        bool ignore)
    {
        if (ignore)
        {
            return;
        }

        for (var index = 0; index < checks.Count; index++)
        {
            if (GetCount(results, $"{namePrefix}_{index}", "duplicateCount") > 0)
            {
                throw new GraphException(checks[index].ErrorMessage);
            }
        }
    }

    private static long GetCount(
        IReadOnlyDictionary<string, AgeBatchResult> results,
        string resultName,
        string column)
    {
        if (!results.TryGetValue(resultName, out var result) || result.Records.Count != 1)
        {
            throw new GraphException($"AGE batch result '{resultName}' was missing or did not contain exactly one record.");
        }

        return result.Records[0][column].As<long>();
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using System.Text;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;


/// <summary>
/// Creates a node–relationship–node subgraph in a single Cypher statement (one driver round-trip):
/// both endpoint nodes, each endpoint's complex-property value-node subtree, and the connecting
/// edge. Reuses the serializer, complex-property tree walk, and identifier escaping shared with the
/// per-entity managers so the persisted shape is identical.
/// </summary>
internal sealed class Neo4jSubgraphManager(GraphContext context)
{
    private readonly EntityFactory _serializer = new(context.LoggerFactory);

    /// <summary>The composed single statement plus its parameters.</summary>
    internal sealed record SubgraphStatement(string Cypher, IDictionary<string, object> Parameters);

    private sealed record EndpointBinding(EntityInfo? Entity, string? ElementId)
    {
        public static EndpointBinding New(EntityInfo entity) => new(entity, ElementId: null);

        public static EndpointBinding Selected(string elementId) => new(Entity: null, elementId);
    }

    internal Task CreateRelationshipAsync(
        string sourceElementId,
        Graph.IRelationship relationship,
        string targetElementId,
        RelationshipDirection direction,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ExecuteElementBoundStatementAsync(
            BuildElementBoundStatement(
                EndpointBinding.Selected(sourceElementId),
                relationship,
                EndpointBinding.Selected(targetElementId),
                direction,
                selfLoop: false),
            transaction,
            cancellationToken);

    internal Task CreateAsync(
        string sourceElementId,
        Graph.IRelationship relationship,
        Graph.INode newTarget,
        RelationshipDirection direction,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ExecuteElementBoundStatementAsync(
            BuildElementBoundStatement(
                EndpointBinding.Selected(sourceElementId),
                relationship,
                EndpointBinding.New(SerializeNode(newTarget)),
                direction,
                selfLoop: false),
            transaction,
            cancellationToken);

    internal Task CreateAsync(
        Graph.INode newSource,
        Graph.IRelationship relationship,
        string targetElementId,
        RelationshipDirection direction,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ExecuteElementBoundStatementAsync(
            BuildElementBoundStatement(
                EndpointBinding.New(SerializeNode(newSource)),
                relationship,
                EndpointBinding.Selected(targetElementId),
                direction,
                selfLoop: false),
            transaction,
            cancellationToken);

    internal Task CreateAsync(
        Graph.INode newSource,
        Graph.IRelationship relationship,
        Graph.INode newTarget,
        RelationshipDirection direction,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ExecuteElementBoundStatementAsync(
            BuildElementBoundStatement(
                EndpointBinding.New(SerializeNode(newSource)),
                relationship,
                EndpointBinding.New(SerializeNode(newTarget)),
                direction,
                selfLoop: false),
            transaction,
            cancellationToken);

    internal Task CreateSelfLoopAsync(
        Graph.INode node,
        Graph.IRelationship relationship,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ExecuteElementBoundStatementAsync(
            BuildElementBoundStatement(
                EndpointBinding.New(SerializeNode(node)),
                relationship,
                target: null,
                RelationshipDirection.Outgoing,
                selfLoop: true),
            transaction,
            cancellationToken);

    private static async Task ExecuteElementBoundStatementAsync(
        SubgraphStatement statement,
        GraphTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var result = await transaction.Transaction
                .RunAsync(statement.Cypher, statement.Parameters)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            var records = await result.ToListAsync(cancellationToken).ConfigureAwait(false);
            if (records.Count != 1 || !records[0]["created"].As<bool>())
            {
                throw new GraphException("The native-bound Neo4j subgraph command did not create one relationship.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GraphException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GraphException("Failed to create the Neo4j subgraph.", ex);
        }
    }

    private EntityInfo SerializeNode<TNode>(TNode node) where TNode : class, Graph.INode
    {
        GraphDataModel.EnsureNoReferenceCycle(node);
        GraphDataModel.EnsureComplexPropertyDepth(node);
        context.NodeManager.ValidateNodeProperties(node);
        return _serializer.Serialize(node);
    }

    private EntityInfo SerializeRelationship<TRelationship>(TRelationship relationship)
        where TRelationship : class, Graph.IRelationship
    {
        GraphDataModel.EnsureNoReferenceCycle(relationship);
        GraphDataModel.EnsureComplexPropertyDepth(relationship);
        context.RelationshipManager.ValidateRelationshipProperties(relationship);

        var entity = _serializer.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException(
                $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
        }

        return entity;
    }

    private SubgraphStatement BuildElementBoundStatement(
        EndpointBinding source,
        Graph.IRelationship relationship,
        EndpointBinding? target,
        RelationshipDirection direction,
        bool selfLoop)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        if (!selfLoop)
        {
            ArgumentNullException.ThrowIfNull(target);
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        var relationshipEntity = SerializeRelationship(relationship);
        var parameters = new Dictionary<string, object>();
        var builder = new StringBuilder();

        // Cypher requires all endpoint MATCH clauses before any CREATE clause. Emit the selected
        // endpoints first, then create whichever endpoints are new.
        if (source.ElementId is not null)
        {
            AppendElementBoundEndpoint(builder, parameters, "s", source);
        }

        if (!selfLoop && target!.ElementId is not null)
        {
            AppendElementBoundEndpoint(builder, parameters, "t", target);
        }

        if (source.ElementId is null)
        {
            AppendElementBoundEndpoint(builder, parameters, "s", source);
        }

        if (!selfLoop && target!.ElementId is null)
        {
            AppendElementBoundEndpoint(builder, parameters, "t", target);
        }

        AppendElementBoundEdge(
            builder,
            parameters,
            relationshipEntity,
            direction,
            selfLoop ? "s" : "t");
        builder.Append("RETURN r IS NOT NULL AS created\n");
        return new SubgraphStatement(builder.ToString(), parameters);
    }

    private static void AppendElementBoundEndpoint(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        string variable,
        EndpointBinding endpoint)
    {
        if (endpoint.ElementId is { } elementId)
        {
            parameters[$"{variable}_elementId"] = elementId;
            builder.Append("MATCH (").Append(variable).Append(") WHERE elementId(")
                .Append(variable).Append(") = $").Append(variable).Append("_elementId\n");
            return;
        }

        var entity = endpoint.Entity ?? throw new GraphException("A new Neo4j endpoint requires a serialized entity.");
        var labelClause = BuildNodeLabelClause(entity);
        var labels = labelClause is null ? string.Empty : $":{labelClause}";
        parameters[$"{variable}_props"] = Neo4jNodeManager.BuildElementBoundNodeProperties(entity);
        builder.Append("CREATE (").Append(variable).Append(labels)
            .Append(" $").Append(variable).Append("_props)\n");
        AppendValueNodes(builder, parameters, ComplexPropertyManager.CollectValueNodeSpecs(variable, entity));
    }

    private static void AppendElementBoundEdge(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        EntityInfo entity,
        RelationshipDirection direction,
        string targetVariable)
    {
        var relationshipType = CypherIdentifier.Escape(entity.Label, "relationship type");
        var (sourceVariable, endVariable) = direction switch
        {
            RelationshipDirection.Outgoing => ("s", targetVariable),
            RelationshipDirection.Incoming => (targetVariable, "s"),
            _ => throw new GraphException($"Unsupported relationship direction '{direction}'.")
        };
        parameters["rel_props"] = Neo4jRelationshipManager.BuildElementBoundRelationshipProperties(entity);
        builder.Append("CREATE (").Append(sourceVariable).Append(")-[r:").Append(relationshipType)
            .Append(" $rel_props]->(").Append(endVariable).Append(")\n");
    }

    private static void AppendValueNodes(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        IReadOnlyList<ComplexPropertyManager.ValueNodeSpec> specs)
    {
        foreach (var spec in specs)
        {
            var relationshipType = CypherIdentifier.Escape(spec.RelationshipType, "complex-property relationship type");
            var label = CypherIdentifier.Escape(spec.Label, "complex-property node label");

            builder.Append("CREATE (").Append(spec.ParentVariable).Append(")-[:").Append(relationshipType)
                .Append(" $").Append(spec.Variable).Append("_rel]->(")
                .Append(spec.Variable).Append(':').Append(label)
                .Append(" $").Append(spec.Variable).Append("_props)\n");

            parameters[$"{spec.Variable}_props"] = spec.NodeProperties;
            parameters[$"{spec.Variable}_rel"] = spec.RelationshipProperties;
        }
    }

    private static string? BuildNodeLabelClause(EntityInfo entity)
    {
        if (entity.ActualType.IsAssignableTo(typeof(Graph.DynamicNode)))
        {
            return entity.ActualLabels is { Count: > 0 }
                ? CypherIdentifier.EscapeLabels(entity.ActualLabels)
                : null;
        }

        return CypherIdentifier.Escape(entity.Label, "node label");
    }

}

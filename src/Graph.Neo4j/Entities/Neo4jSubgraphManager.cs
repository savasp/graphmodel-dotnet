// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using System.Text;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Creates a node–relationship–node subgraph in a single Cypher statement (one driver round-trip):
/// both endpoint nodes, each endpoint's complex-property value-node subtree, and the connecting
/// edge. Reuses the serializer, complex-property tree walk, and identifier escaping shared with the
/// per-entity managers so the persisted shape is identical.
/// </summary>
internal sealed class Neo4jSubgraphManager(GraphContext context)
{
    private readonly ILogger<Neo4jSubgraphManager> _logger = context.LoggerFactory?.CreateLogger<Neo4jSubgraphManager>()
        ?? NullLogger<Neo4jSubgraphManager>.Instance;
    private readonly EntityFactory _serializer = new(context.LoggerFactory);

    /// <summary>
    /// Transient marker recorded on an endpoint <c>ON CREATE</c> so its complex-property subtree is
    /// created only when this statement actually created the endpoint (not when it matched an
    /// existing one). It is removed within the same statement, so it is never committed or read.
    /// </summary>
    private const string TransientCreatedMarker = "__graphModelSubgraphCreated";

    private static readonly string[] _ignoredRelationshipProperties =
    [
        nameof(Graph.IRelationship.StartNodeId),
        nameof(Graph.IRelationship.EndNodeId)
    ];

    /// <summary>The composed single statement plus its parameters.</summary>
    internal sealed record SubgraphStatement(string Cypher, IDictionary<string, object> Parameters);

    public async Task CreateSubgraphAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        bool createMissingEndpoints,
        GraphTransaction transaction,
        CancellationToken cancellationToken = default)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        var statement = BuildStatement(source, relationship, target, createMissingEndpoints);

        _logger.LogDebugNeo4jSubgraphManager44(typeof(TSource).Name, typeof(TRelationship).Name, typeof(TTarget).Name);

        var result = await transaction.Transaction
            .RunAsync(statement.Cypher, statement.Parameters).ConfigureAwait(false);

        // Consume the summary so failures (e.g. a uniqueness-constraint violation on a duplicate
        // endpoint id) surface here rather than being silently swallowed. The whole statement is
        // one Neo4j unit, so a failure creates nothing.
        await result.ConsumeAsync().ConfigureAwait(false);

        _logger.LogInformationNeo4jSubgraphManager54(relationship.Id);
    }

    /// <summary>
    /// Composes the single subgraph-create statement. Exposed internally so its one-statement shape
    /// is directly testable without a live Neo4j instance.
    /// </summary>
    internal SubgraphStatement BuildStatement<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        bool createMissingEndpoints)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        // Serialize and validate each element the same way the per-entity managers do.
        var sourceEntity = SerializeNode(source);
        var targetEntity = SerializeNode(target);
        var relationshipEntity = SerializeRelationship(relationship);

        var parameters = new Dictionary<string, object>();
        var builder = new StringBuilder();

        AppendEndpoint(builder, parameters, "s", sourceEntity, createMissingEndpoints, source.Id);
        AppendEndpoint(builder, parameters, "t", targetEntity, createMissingEndpoints, target.Id);
        AppendEdge(builder, parameters, relationshipEntity, relationship.Direction);

        return new SubgraphStatement(builder.ToString(), parameters);
    }

    private EntityInfo SerializeNode<TNode>(TNode node) where TNode : class, Graph.INode
    {
        GraphDataModel.EnsureNoReferenceCycle(node);
        GraphDataModel.EnsureComplexPropertyDepth(node);
        return _serializer.Serialize(node);
    }

    private EntityInfo SerializeRelationship<TRelationship>(TRelationship relationship)
        where TRelationship : class, Graph.IRelationship
    {
        GraphDataModel.EnsureNoReferenceCycle(relationship);
        GraphDataModel.EnsureComplexPropertyDepth(relationship);

        var entity = _serializer.Serialize(relationship);
        if (entity.ComplexProperties.Count > 0)
        {
            throw new GraphException(
                $"Relationships cannot have complex properties. Found {entity.ComplexProperties.Count} complex properties on {typeof(TRelationship).Name}");
        }

        return entity;
    }

    private static void AppendEndpoint(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        string variable,
        EntityInfo entity,
        bool createMissingEndpoints,
        string id)
    {
        var labelClause = BuildNodeLabelClause(entity);
        parameters[$"{variable}_props"] = BuildNodeProperties(entity);
        var specs = ComplexPropertyManager.CollectValueNodeSpecs(variable, entity);

        if (createMissingEndpoints)
        {
            // MERGE by Id: a matched endpoint is reused entirely as-is — ON CREATE SET never runs on
            // a match, so neither its simple properties nor its existing complex-property subtree are
            // touched. A freshly created endpoint gets its full properties/labels, plus a transient
            // marker that gates the subtree creation below so a matched endpoint's subtree is never
            // duplicated.
            parameters[$"{variable}_id"] = id;
            var setLabels = labelClause is null ? string.Empty : $"{variable}:{labelClause}, ";
            var setMarker = specs.Count > 0 ? $", {variable}.{TransientCreatedMarker} = true" : string.Empty;
            builder.Append("MERGE (").Append(variable).Append(" {Id: $").Append(variable).Append("_id}) ")
                .Append("ON CREATE SET ").Append(setLabels)
                .Append(variable).Append(" = $").Append(variable).Append("_props").Append(setMarker).Append('\n');

            AppendGuardedValueNodes(builder, parameters, variable, specs);
        }
        else
        {
            var labels = labelClause is null ? string.Empty : $":{labelClause}";
            builder.Append("CREATE (").Append(variable).Append(labels)
                .Append(" $").Append(variable).Append("_props)\n");

            AppendValueNodes(builder, parameters, specs);
        }
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

    /// <summary>
    /// Emits the endpoint's complex-property subtree so it is created only when this statement
    /// created the endpoint (see <see cref="TransientCreatedMarker"/>). The whole subtree — arbitrary
    /// nesting depth — is one CREATE of comma-separated patterns inside a single FOREACH: each value
    /// node is bound to a variable a later pattern reuses as its parent, so BFS order (parent before
    /// child) keeps the references valid. The marker is removed afterwards so it never persists.
    /// </summary>
    private static void AppendGuardedValueNodes(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        string variable,
        IReadOnlyList<ComplexPropertyManager.ValueNodeSpec> specs)
    {
        if (specs.Count == 0)
        {
            return;
        }

        builder.Append("FOREACH (_ IN CASE WHEN coalesce(").Append(variable).Append('.').Append(TransientCreatedMarker)
            .Append(", false) THEN [1] ELSE [] END |\n  CREATE ");

        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var relationshipType = CypherIdentifier.Escape(spec.RelationshipType, "complex-property relationship type");
            var label = CypherIdentifier.Escape(spec.Label, "complex-property node label");

            if (i > 0)
            {
                builder.Append(",\n    ");
            }

            builder.Append('(').Append(spec.ParentVariable).Append(")-[:").Append(relationshipType)
                .Append(" $").Append(spec.Variable).Append("_rel]->(")
                .Append(spec.Variable).Append(':').Append(label)
                .Append(" $").Append(spec.Variable).Append("_props)");

            parameters[$"{spec.Variable}_props"] = spec.NodeProperties;
            parameters[$"{spec.Variable}_rel"] = spec.RelationshipProperties;
        }

        builder.Append("\n)\n");
        builder.Append("REMOVE ").Append(variable).Append('.').Append(TransientCreatedMarker).Append('\n');
    }

    private static void AppendEdge(
        StringBuilder builder,
        Dictionary<string, object> parameters,
        EntityInfo entity,
        RelationshipDirection direction)
    {
        var relationshipType = CypherIdentifier.Escape(entity.Label, "relationship type");

        // The endpoint variables are "s" (source) and "t" (target). The stored edge points from
        // source to target for Outgoing, and from target to source for Incoming.
        var (sourceVariable, targetVariable) = direction switch
        {
            RelationshipDirection.Outgoing => ("s", "t"),
            RelationshipDirection.Incoming => ("t", "s"),
            _ => throw new GraphException($"Unsupported relationship direction '{direction}'.")
        };

        var properties = SerializationHelpers.SerializeSimpleProperties(entity)
            .Where(kv => !_ignoredRelationshipProperties.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        properties[nameof(Graph.IRelationship.Type)] = entity.Label;

        parameters["rel_props"] = properties;

        builder.Append("CREATE (").Append(sourceVariable).Append(")-[r:").Append(relationshipType)
            .Append(" $rel_props]->(").Append(targetVariable).Append(")\n");
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

    private static Dictionary<string, object?> BuildNodeProperties(EntityInfo entity)
    {
        var properties = SerializationHelpers.SerializeSimpleProperties(entity);
        properties[SerializationBridge.EntityKindPropertyName] = SerializationBridge.NodeEntityKind;
        properties[nameof(Graph.INode.Labels)] =
            entity.ActualLabels is null || entity.ActualLabels.Count == 0 ? [entity.Label] : entity.ActualLabels;
        return properties;
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Text;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Neo4j.Entities;

namespace Cvoya.Graph.Neo4j.Querying.Cypher;

/// <summary>
/// Defines Neo4j's Cypher syntax and supported graph-query capabilities.
/// </summary>
public sealed class Neo4jDialect : ICypherDialect
{
    private static readonly CapabilitySet SupportedCapabilities = CapabilitySet.Of(
        GraphCapability.FullTextSearch,
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade,
        GraphCapability.CallSubqueries,
        GraphCapability.PatternSizeProjection,
        GraphCapability.MultiLabelMatch,
        GraphCapability.OrderByEntity,
        GraphCapability.OptionalTraversal);

    /// <summary>Gets the shared Neo4j dialect instance.</summary>
    public static Neo4jDialect Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "Neo4j";

    /// <inheritdoc/>
    public CapabilitySet Capabilities => SupportedCapabilities;

    // The Neo4j full-text scaffolding — the procedure/index names and the mixed-entity subquery
    // shape — is private to this dialect; the shared renderer delegates the whole clause here.
    private const string FullTextNodeProcedure = "db.index.fulltext.queryNodes";
    private const string FullTextRelationshipProcedure = "db.index.fulltext.queryRelationships";
    private const string FullTextNodeIndex = "node_fulltext_index";
    private const string FullTextRelationshipIndex = "rel_fulltext_index";

    /// <inheritdoc/>
    public string ComplexPropertyRelationshipMarker => ComplexPropertyStorage.RelationshipMarkerProperty;

    /// <inheritdoc/>
    public string RenderParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return $"${name}";
    }

    /// <inheritdoc/>
    public string RenderPropertyAccess(string target, string property, bool escape)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return $"{target}.{(escape ? CypherIdentifier.Escape(property, "property name") : property)}";
    }

    /// <inheritdoc/>
    public string RenderFunctionName(string function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(function);
        return function switch
        {
            "temporal.datetime" => "datetime",
            "temporal.localDateTime" => "localdatetime",
            "temporal.date" => "date",
            "temporal.time" => "time",
            "temporal.duration" => "duration",
            "string.join" => "apoc.text.join",
            "string.indexOf" => "apoc.text.indexOf",
            "string.lastIndexOf" => "apoc.text.lastIndexOf",
            "string.padLeft" => "apoc.text.lpad",
            "string.padRight" => "apoc.text.rpad",
            "string.compareTo" => "apoc.text.compareTo",
            _ => function,
        };
    }

    /// <inheritdoc/>
    public CypherFunctionBehavior GetFunctionBehavior(string function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(function);
        return CypherFunctionBehavior.Render;
    }

    /// <inheritdoc/>
    public string RenderNodeLabels(IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return string.Join('|', labels.Select(label => CypherIdentifier.EscapeIfNeeded(label, "node label")));
    }

    /// <inheritdoc/>
    public string RenderRelationshipTypes(IReadOnlyList<string> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return string.Join('|', types.Select(type => CypherIdentifier.EscapeIfNeeded(type, "relationship type")));
    }

    /// <inheritdoc/>
    public string RenderDepth(DepthRange depth)
    {
        ArgumentNullException.ThrowIfNull(depth);
        return $"*{depth.Min}..{depth.Max}";
    }

    /// <inheritdoc/>
    public string RenderLabelTest(string target, IReadOnlyList<string> labels, Func<string, string> renderLiteral)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(renderLiteral);

        var conditions = labels.Select(item => $"{renderLiteral(item)} IN labels({target})").ToArray();
        return conditions.Length == 1 ? conditions[0] : $"({string.Join(" OR ", conditions)})";
    }

    /// <inheritdoc/>
    public string RenderFullTextSearch(FullTextSearchClause clause, ICypherRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(clause);
        ArgumentNullException.ThrowIfNull(context);

        if (clause.Target == Cvoya.Graph.Querying.SearchRootTarget.Entities)
        {
            return RenderEntitySearch(clause, context);
        }

        var (procedure, index, yieldedName) = clause.Target switch
        {
            Cvoya.Graph.Querying.SearchRootTarget.Nodes =>
                (FullTextNodeProcedure, FullTextNodeIndex, "node"),
            Cvoya.Graph.Querying.SearchRootTarget.Relationships =>
                (FullTextRelationshipProcedure, FullTextRelationshipIndex, "relationship"),
            _ => throw new GraphException($"Unsupported full-text search target '{clause.Target}'."),
        };

        return new StringBuilder()
            .Append("CALL ")
            .Append(procedure)
            .Append('(')
            .Append(context.RenderLiteral(index))
            .Append(", ")
            .Append(context.RenderExpression(clause.Query))
            .Append(") YIELD ")
            .Append(yieldedName)
            .Append(" AS ")
            .Append(clause.Alias)
            .ToString();
    }

    private static string RenderEntitySearch(FullTextSearchClause search, ICypherRenderContext context)
    {
        return new StringBuilder()
            .Append("CALL {\n")
            .Append("    CALL ").Append(FullTextNodeProcedure).Append('(')
            .Append(context.RenderLiteral(FullTextNodeIndex)).Append(", ")
            .Append(context.RenderExpression(search.Query)).Append(") YIELD node\n")
            .Append("    RETURN node AS entity\n")
            .Append("    UNION ALL\n")
            .Append("    CALL ").Append(FullTextRelationshipProcedure).Append('(')
            .Append(context.RenderLiteral(FullTextRelationshipIndex)).Append(", ")
            .Append(context.RenderExpression(search.Query)).Append(") YIELD relationship\n")
            .Append("    MATCH (src)-[relationship]->(tgt)\n")
            .Append("    RETURN { StartNode: { Node: src, ComplexProperties: [] }, ")
            .Append("Relationship: relationship, EndNode: { Node: tgt, ComplexProperties: [] } } AS entity\n")
            .Append('}')
            .ToString();
    }
}

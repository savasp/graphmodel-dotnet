// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

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

    /// <inheritdoc/>
    public string FullTextNodeProcedure => "db.index.fulltext.queryNodes";

    /// <inheritdoc/>
    public string FullTextRelationshipProcedure => "db.index.fulltext.queryRelationships";

    /// <inheritdoc/>
    public string FullTextNodeIndex => "node_fulltext_index";

    /// <inheritdoc/>
    public string FullTextRelationshipIndex => "rel_fulltext_index";

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
}

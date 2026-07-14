// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Cypher;
using Cvoya.Graph.Cypher.Ast;

namespace Cvoya.Graph.Age.Querying.Cypher;

/// <summary>
/// Defines Apache AGE's Cypher syntax and supported graph-query capabilities.
/// </summary>
public sealed class AgeDialect : ICypherDialect
{
    private static readonly CapabilitySet SupportedCapabilities = CapabilitySet.Of(
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade,
        GraphCapability.MultiLabelMatch,
        GraphCapability.OrderByEntity,
        GraphCapability.OptionalTraversal,
        GraphCapability.FullTextSearch);

    /// <summary>Initializes the Apache AGE dialect.</summary>
    public AgeDialect()
    {
    }

    /// <summary>Gets the shared Apache AGE dialect instance.</summary>
    public static AgeDialect Instance { get; } = new();

    internal static AgeDialect PlanningInstance { get; } = Instance;

    /// <inheritdoc/>
    public string Name => "Apache AGE";

    /// <inheritdoc/>
    /// <remarks>
    /// AGE-compatible lowering implements multi-label matches, entity ordering, and optional
    /// traversal before rendering. Full-text search is lowered earlier still, at the expression level,
    /// to a two-phase Postgres text-search query (<see cref="Querying.AgeFullTextSearch"/>).
    /// Capabilities describe user-visible behavior, whether native or lowered, so both the planning and
    /// public instances declare those features.
    /// </remarks>
    public CapabilitySet Capabilities => SupportedCapabilities;

    /// <inheritdoc/>
    public string FullTextNodeProcedure => throw FullTextNotSupported();

    /// <inheritdoc/>
    public string FullTextRelationshipProcedure => throw FullTextNotSupported();

    /// <inheritdoc/>
    public string FullTextNodeIndex => throw FullTextNotSupported();

    /// <inheritdoc/>
    public string FullTextRelationshipIndex => throw FullTextNotSupported();

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
            "math.ceiling" => "ceil",
            _ => function,
        };
    }

    /// <inheritdoc/>
    public CypherFunctionBehavior GetFunctionBehavior(string function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(function);
        return function switch
        {
            "string.join" or
            "string.indexOf" or
            "string.lastIndexOf" or
            "string.padLeft" or
            "string.padRight" or
            "string.compareTo" => CypherFunctionBehavior.Unsupported,
            _ => CypherFunctionBehavior.Render,
        };
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

        var conditions = labels.Select(item =>
            $"({renderLiteral(item)} IN labels({target}) OR " +
            $"{renderLiteral(item)} IN coalesce({target}.inheritance_labels, []))").ToArray();
        return conditions.Length == 1 ? conditions[0] : $"({string.Join(" OR ", conditions)})";
    }

    private static GraphQueryTranslationException FullTextNotSupported() => new(
        $"The Apache AGE dialect does not support capability {GraphCapability.FullTextSearch}.");
}

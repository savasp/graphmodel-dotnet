// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast;

namespace Cvoya.Graph.Cypher.Tests;

internal sealed class TestCypherDialect(
    CapabilitySet capabilities,
    string name = "TestCypher",
    IReadOnlySet<string>? clientEvaluatedFunctions = null,
    IReadOnlySet<string>? unsupportedFunctions = null) : ICypherDialect
{
    public static TestCypherDialect Full { get; } = new(CapabilitySet.All);

    public string Name { get; } = name;

    public CapabilitySet Capabilities { get; } = capabilities;

    // No RenderFullTextSearch override: this dialect inherits the throwing default, so it doubles
    // as the "dialect that does not support full-text search" fixture (see CypherRendererTests).
    public string ComplexPropertyRelationshipMarker => "__testComplexProperty";

    public string RenderParameter(string name) => $"${name}";

    public string RenderPropertyAccess(string target, string property, bool escape) => $"{target}.{property}";

    public string RenderFunctionName(string function) => function;

    public CypherFunctionBehavior GetFunctionBehavior(string function) =>
        unsupportedFunctions?.Contains(function) == true
            ? CypherFunctionBehavior.Unsupported
            : clientEvaluatedFunctions?.Contains(function) == true
                ? CypherFunctionBehavior.EvaluateOnClient
                : CypherFunctionBehavior.Render;

    public string RenderNodeLabels(IReadOnlyList<string> labels) => string.Join('|', labels);

    public string RenderRelationshipTypes(IReadOnlyList<string> types) => string.Join('|', types);

    public string RenderDepth(DepthRange depth) => depth.Max == int.MaxValue
        ? $"*{depth.Min}.."
        : $"*{depth.Min}..{depth.Max}";

    public string RenderLabelTest(string target, IReadOnlyList<string> labels, Func<string, string> renderLiteral) =>
        string.Join(" OR ", labels.Select(label => $"{renderLiteral(label)} IN labels({target})"));
}

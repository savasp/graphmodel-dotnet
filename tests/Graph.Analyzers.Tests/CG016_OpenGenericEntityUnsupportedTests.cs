// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG016_OpenGenericEntityUnsupportedTests
{
    [Fact]
    public async Task OpenGenericNode_ProducesDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record {|#0:GenericNode|}<T> : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = Error().WithLocation(0).WithArguments("GenericNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task OpenGenericRelationship_ProducesDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record {|#0:GenericLink|}<T>(string StartNodeId, string EndNodeId)
                : Relationship(StartNodeId, EndNodeId);
            """;

        var expected = Error().WithLocation(0).WithArguments("GenericLink");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task EntityNestedInOpenGenericType_ProducesDiagnostic()
    {
        // A non-generic entity nested in an open generic captures the containing unbound parameter.
        const string test = """
            using Cvoya.Graph;

            public class Container<T>
            {
                public record {|#0:Inner|} : Node
                {
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        var expected = Error().WithLocation(0).WithArguments("Inner");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task AbstractOpenGenericBaseWithClosedSubtype_DoesNotProduceDiagnostic()
    {
        // Non-goal: an abstract generic base inherited only by a closed, non-generic concrete entity
        // is supported and must not be flagged, and neither is the closed subtype.
        const string test = """
            using Cvoya.Graph;

            public abstract record GenericNode<T> : Node
            {
                public string Name { get; set; } = string.Empty;
            }

            [Node("StringNode")]
            public record StringNode : GenericNode<string>;
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonGenericEntity_DoesNotProduceDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            [Node("Person")]
            public record Person : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    private static DiagnosticResult Error() =>
        new("CG016", DiagnosticSeverity.Error);
}

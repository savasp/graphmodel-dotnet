// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG012_MisappliedNodeOrRelationshipAttributeTests
{
    [Fact]
    public async Task NodeAttributeOnClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [Node("Person")]
            public class PersonNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipAttributeOnClassImplementingIRelationship_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [Relationship("FOLLOWS")]
            public class FollowsRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeAttributeOnPlainClass_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            public class PersonData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("PersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipAttributeOnPlainClass_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [{|#0:Relationship("FOLLOWS")|}]
            public class FollowsData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("FollowsData", "Relationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnClassImplementingOnlyIRelationship_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            public class MislabeledRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("MislabeledRelationship", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipAttributeOnClassImplementingOnlyINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [{|#0:Relationship("FOLLOWS")|}]
            public class MislabeledNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("MislabeledNode", "Relationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnRecordImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [Node("Person")]
            public record PersonNode : Node
            {
                public string Name { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeAttributeOnPlainRecord_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            public record PersonData
            {
                public string Name { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("PersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnAbstractClassNotImplementingINode_ProducesDiagnostic()
    {
        // Abstract types are exempt from CG011 (inherit-from-base-class), but not from CG012 -
        // the attribute is still a no-op if the abstract type never implements INode itself.
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            public abstract class AbstractPersonData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG012")
            .WithLocation(0)
            .WithArguments("AbstractPersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnAbstractClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            [Node("Person")]
            public abstract record AbstractPersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeAttributeNotRedeclaredOnDerivedClass_NoDiagnosticForDerivedType()
    {
        // NodeAttribute is Inherited = true, so a derived class picks up the base [Node] via
        // reflection/metadata even without re-declaring it - but INamedTypeSymbol.GetAttributes()
        // only returns attributes declared directly on the symbol being analyzed, never inherited
        // ones. So a derived class that implements INode (via the Node base class) and doesn't
        // redeclare [Node] itself is never examined for CG012 at all - there's no attribute
        // application on DerivedPersonNode's own declaration for CG012 to inspect.
        var test = """
            using Cvoya.Graph;

            [Node("Person")]
            public class BasePersonNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }

            public class DerivedPersonNode : BasePersonNode
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

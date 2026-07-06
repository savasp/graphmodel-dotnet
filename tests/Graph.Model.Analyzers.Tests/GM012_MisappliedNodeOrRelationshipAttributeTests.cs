// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Model.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class GM012_MisappliedNodeOrRelationshipAttributeTests
{
    [Fact]
    public async Task NodeAttributeOnClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

            [{|#0:Node("Person")|}]
            public class PersonData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("PersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipAttributeOnPlainClass_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;

            [{|#0:Relationship("FOLLOWS")|}]
            public class FollowsData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("FollowsData", "Relationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnClassImplementingOnlyIRelationship_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;

            [{|#0:Node("Person")|}]
            public class MislabeledRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("MislabeledRelationship", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipAttributeOnClassImplementingOnlyINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;

            [{|#0:Relationship("FOLLOWS")|}]
            public class MislabeledNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("MislabeledNode", "Relationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnRecordImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;

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
            using Cvoya.Graph.Model;

            [{|#0:Node("Person")|}]
            public record PersonData
            {
                public string Name { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("PersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnAbstractClassNotImplementingINode_ProducesDiagnostic()
    {
        // Abstract types are exempt from GM011 (inherit-from-base-class), but not from GM012 -
        // the attribute is still a no-op if the abstract type never implements INode itself.
        var test = """
            using Cvoya.Graph.Model;

            [{|#0:Node("Person")|}]
            public abstract class AbstractPersonData
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM012")
            .WithLocation(0)
            .WithArguments("AbstractPersonData", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeAttributeOnAbstractClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
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
        // redeclare [Node] itself is never examined for GM012 at all - there's no attribute
        // application on DerivedPersonNode's own declaration for GM012 to inspect.
        var test = """
            using Cvoya.Graph.Model;

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

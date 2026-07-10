// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG013_ConflictingNodeAndRelationshipAttributesTests
{
    [Fact]
    public async Task NodeAttributeOnly_NoDiagnostic()
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
    public async Task RelationshipAttributeOnly_NoDiagnostic()
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
    public async Task BothAttributesOnTypeImplementingBothInterfaces_ProducesOnlyConflictDiagnostic()
    {
        // A type implementing both INode and IRelationship is unusual but not prevented by the
        // type system - using one here isolates CG013 from CG012 (which only fires when an
        // attribute's matching interface is missing) and from CG011 (both base-class-inheritance
        // warnings are still expected, since this type implements the interfaces directly).
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            [Node("Person")]
            [Relationship("FOLLOWS")]
            public class {|#0:Ambiguous|} : INode, IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
                public string Type { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("Ambiguous", "Node", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("Ambiguous", "Relationship", "IRelationship"),
            VerifyCS.Diagnostic("CG013").WithLocation(0).WithArguments("Ambiguous"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task BothAttributesOnTypeImplementingNeitherInterface_ProducesConflictAndMisappliedDiagnostics()
    {
        // Neither attribute matches an implemented interface here, so CG012 fires for each
        // attribute in addition to CG013 for the conflict itself - all three are legitimate,
        // independent findings about the same broken declaration.
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            [{|#1:Relationship("FOLLOWS")|}]
            public class {|#2:Ambiguous|}
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCG012.Diagnostic("CG012").WithLocation(0).WithArguments("Ambiguous", "Node", "INode"),
            VerifyCG012.Diagnostic("CG012").WithLocation(1).WithArguments("Ambiguous", "Relationship", "IRelationship"),
            VerifyCS.Diagnostic("CG013").WithLocation(2).WithArguments("Ambiguous"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task BothAttributesOnRecordImplementingBothInterfaces_ProducesOnlyConflictDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            [Node("Person")]
            [Relationship("FOLLOWS")]
            public record {|#0:Ambiguous|} : INode, IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
                public string Type { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("Ambiguous", "Node", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("Ambiguous", "Relationship", "IRelationship"),
            VerifyCS.Diagnostic("CG013").WithLocation(0).WithArguments("Ambiguous"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedefs for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}

file static class VerifyCG012
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

file static class VerifyCG011
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

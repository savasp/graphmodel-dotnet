// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG014_EntityTypeMustBeReferenceTypeTests
{
    [Fact]
    public async Task StructImplementingINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public struct {|#0:PersonStruct|} : INode
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "PersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("PersonStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StructImplementingIRelationship_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public struct {|#0:FollowsStruct|} : IRelationship
            {
                public string Id { get; init; }
                public string Type { get; init; }
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; }
                public string EndNodeId { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "FollowsStruct", "IRelationship"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("FollowsStruct", "Relationship", "IRelationship"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RecordStructImplementingINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public record struct {|#0:PersonRecordStruct|} : INode
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Record struct", "PersonRecordStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("PersonRecordStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ReadonlyStructImplementingINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public readonly struct {|#0:PersonStruct|} : INode
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "PersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("PersonStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StructImplementingDerivedNodeInterface_ProducesDiagnostic()
    {
        // IPerson derives from INode without directly naming it - the analyzer must walk the full
        // interface chain (AllInterfaces), not just the type's directly-listed interfaces.
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public interface IPerson : INode
            {
                string Name { get; }
            }

            public struct {|#0:PersonStruct|} : IPerson
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
                public string Name { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "PersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("PersonStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public class PersonNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RecordClassImplementingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public record PersonNode : Node
            {
                public string Name { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainStructWithNoEntityInterface_NoDiagnostic()
    {
        var test = """
            public struct PlainStruct
            {
                public int Value { get; init; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructUsedAsComplexPropertyTypeOnValidClassNode_NoDiagnostic()
    {
        // A flat struct POCO is a fully supported complex property value shape (see the #145/#152
        // type-classification truth tables) - CG014 must not treat "struct referenced from a node
        // property" the same as "struct implementing INode/IRelationship".
        var test = """
            using Cvoya.Graph;

            public struct Address
            {
                public string Street { get; init; }
                public int Unit { get; init; }
            }

            public class PersonNode : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PartialStructImplementingINode_ProducesExactlyOneDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public partial struct {|#0:PersonStruct|} : INode
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
            }

            public partial struct PersonStruct
            {
                public string Name { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "PersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("PersonStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task GenericStructImplementingINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public struct {|#0:GenericPersonStruct|}<T> : INode
            {
                public string Id { get; init; }
                public IReadOnlyList<string> Labels { get; init; }
                public string Name { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "GenericPersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("GenericPersonStruct", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedStructImplementingINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class Container
            {
                public struct {|#0:NestedPersonStruct|} : INode
                {
                    public string Id { get; init; }
                    public IReadOnlyList<string> Labels { get; init; }
                }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG014").WithLocation(0).WithArguments("Struct", "NestedPersonStruct", "INode"),
            VerifyCG011.Diagnostic("CG011").WithLocation(0).WithArguments("NestedPersonStruct", "Node", "INode"),
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

file static class VerifyCG011
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

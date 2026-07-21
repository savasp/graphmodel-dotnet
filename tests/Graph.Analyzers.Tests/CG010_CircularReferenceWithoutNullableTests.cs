// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG010_CircularReferenceWithoutNullableTests
{
    [Fact]
    public async Task ValidNodeWithoutCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public Address Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithNullableCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public TreeNode? Parent { get; set; }
                public TreeNode? LeftChild { get; set; }
                public TreeNode? RightChild { get; set; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TreeNode Data { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeWithDirectCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType SelfReference { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public CircularType {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Data", "TestNode", "CircularType");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithIndirectCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TypeA
            {
                public string Name { get; set; } = string.Empty;
                public TypeB Reference { get; set; } = new();
            }
            
            public class TypeB
            {
                public string Value { get; set; } = string.Empty;
                public TypeA BackReference { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TypeA {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Data", "TestNode", "TypeA");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithLongerCircularChain_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TypeA
            {
                public string Name { get; set; } = string.Empty;
                public TypeB Next { get; set; } = new();
            }
            
            public class TypeB
            {
                public string Value { get; set; } = string.Empty;
                public TypeC Next { get; set; } = new();
            }
            
            public class TypeC
            {
                public string Data { get; set; } = string.Empty;
                public TypeA BackToStart { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TypeA {|#0:Chain|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Chain", "TestNode", "TypeA");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithPartialNullableCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TypeA
            {
                public string Name { get; set; } = string.Empty;
                public TypeB Reference { get; set; } = new(); // Non-nullable
            }
            
            public class TypeB
            {
                public string Value { get; set; } = string.Empty;
                public TypeA? BackReference { get; set; } // Nullable
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TypeA Data { get; set; } = new();
            }
            """;

        // A cycle with at least one nullable reference should be considered safe
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeWithCollectionCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class CustomNode
            {
                public string Value { get; set; } = string.Empty;
                public List<CustomNode> Children { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public CustomNode {|#0:Tree|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Tree", "TestNode", "CustomNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithArrayCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public TreeNode[] Children { get; set; } = Array.Empty<TreeNode>();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TreeNode {|#0:Root|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Root", "TestNode", "TreeNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ValidComplexTypeWithNullableCollectionReferences_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public List<TreeNode>? Children { get; set; }
                public TreeNode? Parent { get; set; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public TreeNode Tree { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleCircularTypes_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            
            public class CircularType1
            {
                public string Name { get; set; } = string.Empty;
                public CircularType1 Self { get; set; } = null!;
            }
            
            public class CircularType2
            {
                public string Value { get; set; } = string.Empty;
                public CircularType2 Self { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public CircularType1 {|#0:Data1|} { get; set; } = new();
                public CircularType2 {|#1:Data2|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG010").WithLocation(0).WithArguments("Data1", "TestNode", "CircularType1"),
            VerifyCS.Diagnostic("CG010").WithLocation(1).WithArguments("Data2", "TestNode", "CircularType2")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithCircularComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class CircularMetadata
            {
                public string Key { get; set; } = string.Empty;
                public CircularMetadata NestedData { get; set; } = null!;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public CircularMetadata {|#0:Metadata|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Metadata", "TestRelationship", "CircularMetadata");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeNotUsedInGraphTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType Self { get; set; } = null!;
            }
            
            public class RegularClass
            {
                public CircularType Data { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InheritedNodeWithCircularComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType Self { get; set; } = null!;
            }
            
            public class BaseNode : Node
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public CircularType {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG010")
            .WithLocation(0)
            .WithArguments("Data", "DerivedNode", "CircularType");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RecordWithEqualityContract_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }
            
            [Node(Label = "Person")]
            public record PersonRecord : Node
            {
                public string Name { get; set; } = string.Empty;
                public Address HomeAddress { get; set; } = new();
                public Address? WorkAddress { get; set; }
            }
            """;

        // Records have an auto-generated EqualityContract property that should be ignored
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeWithoutCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
                public string Country { get; set; } = string.Empty;
            }
            
            public class Contact
            {
                public string Email { get; set; } = string.Empty;
                public string Phone { get; set; } = string.Empty;
            }
            
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address HomeAddress { get; set; } = new();
                public Address? WorkAddress { get; set; }
                public List<Address> PreviousAddresses { get; set; } = new();
                public Contact ContactInfo { get; set; } = new();
            }
            """;

        // Complex types like Address and Contact should not trigger circular reference warnings
        // when they don't actually have circular references
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RecordWithComplexNestedTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Nested1
            {
                public string Value1 { get; set; } = string.Empty;
            }
            
            public class Nested2
            {
                public string Value2 { get; set; } = string.Empty;
                public Nested1 NestedProperty { get; set; } = new();
            }
            
            [Node(Label = "ComplexRecord")]
            public record ComplexRecord : Node
            {
                public string Name { get; set; } = string.Empty;
                public Nested2 ComplexData { get; set; } = new();
            }
            """;

        // Records with complex nested types should work fine
        await VerifyAnalyzerAsync(test);
    }
}


// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}
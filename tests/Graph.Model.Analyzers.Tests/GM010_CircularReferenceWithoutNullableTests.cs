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

public class GM010_CircularReferenceWithoutNullableTests
{
    [Fact]
    public async Task ValidNodeWithoutCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public TreeNode? Parent { get; set; }
                public TreeNode? LeftChild { get; set; }
                public TreeNode? RightChild { get; set; }
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType SelfReference { get; set; } = null!;
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public CircularType {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Data", "TestNode", "CircularType");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithIndirectCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
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
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public TypeA {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Data", "TestNode", "TypeA");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithLongerCircularChain_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
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
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public TypeA {|#0:Chain|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Chain", "TestNode", "TypeA");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithPartialNullableCircularReference_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
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
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class Node
            {
                public string Value { get; set; } = string.Empty;
                public List<Node> Children { get; set; } = new();
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public Node {|#0:Tree|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Tree", "TestNode", "Node");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithArrayCircularReference_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public TreeNode[] Children { get; set; } = Array.Empty<TreeNode>();
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public TreeNode {|#0:Root|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Root", "TestNode", "TreeNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ValidComplexTypeWithNullableCollectionReferences_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TreeNode
            {
                public string Value { get; set; } = string.Empty;
                public List<TreeNode>? Children { get; set; }
                public TreeNode? Parent { get; set; }
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
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
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public CircularType1 {|#0:Data1|} { get; set; } = new();
                public CircularType2 {|#1:Data2|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM010").WithLocation(0).WithArguments("Data1", "TestNode", "CircularType1"),
            VerifyCS.Diagnostic("GM010").WithLocation(1).WithArguments("Data2", "TestNode", "CircularType2")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithCircularComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class CircularMetadata
            {
                public string Key { get; set; } = string.Empty;
                public CircularMetadata NestedData { get; set; } = null!;
            }
            
            public class TestRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public CircularMetadata {|#0:Metadata|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM005")
            .WithLocation(0)
            .WithArguments("Metadata", "TestRelationship", "CircularMetadata");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeNotUsedInGraphTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType Self { get; set; } = null!;
            }
            
            public class RegularClass
            {
                public CircularType Data { get; set; } = new();
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class CircularType
            {
                public string Name { get; set; } = string.Empty;
                public CircularType Self { get; set; } = null!;
            }
            
            public class BaseNode : INode
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public CircularType {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM010")
            .WithLocation(0)
            .WithArguments("Data", "DerivedNode", "CircularType");

        await VerifyAnalyzerAsync(test, expected);
    }
}


// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}
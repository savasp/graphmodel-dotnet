// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Cvoya.Graph.Analyzers.GraphModelAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class CG001_MissingParameterlessClassConstructorTests
{
    [Fact]
    public async Task ValidNodeWithParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithExplicitParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public TestNode() { }
                
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithConstructorInitializingAllProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public TestNode(string name)
                {
                    Name = name;
                }
                
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithoutParameterlessConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class {|#0:TestNode|} : INode
            {
                public TestNode(string name)
                {
                    Name = name;
                }
                
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG001")
                .WithLocation(0)
                .WithArguments("TestNode", "INode"),
            VerifyCS.Diagnostic("CG011")
                .WithLocation(0)
                .WithArguments("TestNode", "Node", "INode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithoutParameterlessConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class {|#0:TestRelationship|} : IRelationship
            {
                public TestRelationship(string customProperty)
                {
                    CustomProperty = customProperty;
                }
                
                public string Id { get; init; } = string.Empty;
                public string Type { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public string CustomProperty { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG001")
                .WithLocation(0)
                .WithArguments("TestRelationship", "IRelationship"),
            VerifyCS.Diagnostic("CG011")
                .WithLocation(0)
                .WithArguments("TestRelationship", "Relationship", "IRelationship")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ValidRelationshipWithParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public string Type { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithPrivateParameterlessConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class {|#0:TestNode|} : INode
            {
                private TestNode() { }
                
                public TestNode(string name)
                {
                    Name = name;
                }
                
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG001")
                .WithLocation(0)
                .WithArguments("TestNode", "INode"),
            VerifyCS.Diagnostic("CG011")
                .WithLocation(0)
                .WithArguments("TestNode", "Node", "INode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithInternalParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                internal TestNode() { }
                
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDiagnostic()
    {
        var test = """
            public class RegularClass
            {
                public RegularClass(string name)
                {
                    Name = name;
                }
                
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

}
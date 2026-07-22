// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG003_PropertyCannotBeGraphInterfaceTypeTests
{
    [Fact]
    public async Task ValidNodeWithSimpleProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public int Age { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithINodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public INode {|#0:Parent|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Parent", "TestNode", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithIRelationshipProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public IRelationship {|#0:Connection|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Connection", "TestNode", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConcreteNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public PersonNode {|#0:Person|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Person", "TestNode", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConcreteRelationshipProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class FollowsRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public FollowsRelationship {|#0:FollowsConnection|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("FollowsConnection", "TestNode", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithListOfINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<INode> {|#0:Children|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Children", "TestNode", "List<INode>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithListOfIRelationship_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<IRelationship> {|#0:Relationships|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Relationships", "TestNode", "List<IRelationship>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithArrayOfNodes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public PersonNode[] {|#0:People|} { get; set; } = Array.Empty<PersonNode>();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("People", "TestNode", "PersonNode[]");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithIEnumerableOfNodes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public IEnumerable<INode> {|#0:Nodes|} { get; set; } = Enumerable.Empty<INode>();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Nodes", "TestNode", "IEnumerable<INode>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public INode {|#0:StartNode|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("StartNode", "TestRelationship", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public INode? {|#0:Parent|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG003")
            .WithLocation(0)
            .WithArguments("Parent", "TestNode", "INode?");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithValidComplexType_NoDiagnostic()
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
                public Address Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithListOfSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<string> Tags { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NonSerializedGraphInterfaceProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public sealed class TestNode : Node
            {
                [Property(Ignore = true)]
                public INode IgnoredNode { get; set; } = null!;

                [Property(Ignore = true)]
                public IRelationship IgnoredRelationship { get; set; } = null!;

                public static INode Shared { get; set; } = null!;

                public IRelationship this[int index]
                {
                    get => null!;
                    set { }
                }

                private INode Hidden { get; set; } = null!;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InheritedSerializedGraphInterfaceProperty_ProducesDiagnosticForEachEntity()
    {
        var test = """
            using Cvoya.Graph;

            public abstract class BaseNode : Node
            {
                public INode {|#0:Parent|} { get; set; } = null!;
            }

            public sealed class DerivedNode : BaseNode
            {
            }
            """;

        await VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic("CG003").WithLocation(0).WithArguments("Parent", "BaseNode", "INode"),
            VerifyCS.Diagnostic("CG003").WithLocation(0).WithArguments("Parent", "DerivedNode", "INode"));
    }

    [Fact]
    public async Task MultiplePropertiesWithGraphTypes_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public INode {|#0:Parent|} { get; set; } = null!;
                public IRelationship {|#1:Connection|} { get; set; } = null!;
                public List<INode> {|#2:Children|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG003").WithLocation(0).WithArguments("Parent", "TestNode", "INode"),
            VerifyCS.Diagnostic("CG003").WithLocation(1).WithArguments("Connection", "TestNode", "IRelationship"),
            VerifyCS.Diagnostic("CG003").WithLocation(2).WithArguments("Children", "TestNode", "List<INode>")
        };

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}

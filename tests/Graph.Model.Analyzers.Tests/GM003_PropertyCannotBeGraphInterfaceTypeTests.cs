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

public class GM003_PropertyCannotBeGraphInterfaceTypeTests
{
    [Fact]
    public async Task ValidNodeWithSimpleProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public INode {|#0:Parent|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Parent", "TestNode", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithIRelationshipProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public IRelationship {|#0:Connection|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Connection", "TestNode", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConcreteNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public PersonNode {|#0:Person|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Person", "TestNode", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConcreteRelationshipProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class FollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public FollowsRelationship {|#0:FollowsConnection|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("FollowsConnection", "TestNode", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithListOfINode_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public List<INode> {|#0:Children|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Children", "TestNode", "List<INode>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithListOfIRelationship_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public List<IRelationship> {|#0:Relationships|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Relationships", "TestNode", "List<IRelationship>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithArrayOfNodes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public PersonNode[] {|#0:People|} { get; set; } = Array.Empty<PersonNode>();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("People", "TestNode", "PersonNode[]");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithIEnumerableOfNodes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public IEnumerable<INode> {|#0:Nodes|} { get; set; } = Enumerable.Empty<INode>();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Nodes", "TestNode", "IEnumerable<INode>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public INode {|#0:StartNode|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("StartNode", "TestRelationship", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public INode? {|#0:Parent|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("GM003")
            .WithLocation(0)
            .WithArguments("Parent", "TestNode", "INode?");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithValidComplexType_NoDiagnostic()
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
                public Address Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithListOfSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public List<string> Tags { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultiplePropertiesWithGraphTypes_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public INode {|#0:Parent|} { get; set; } = null!;
                public IRelationship {|#1:Connection|} { get; set; } = null!;
                public List<INode> {|#2:Children|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM003").WithLocation(0).WithArguments("Parent", "TestNode", "INode"),
            VerifyCS.Diagnostic("GM003").WithLocation(1).WithArguments("Connection", "TestNode", "IRelationship"),
            VerifyCS.Diagnostic("GM003").WithLocation(2).WithArguments("Children", "TestNode", "List<INode>")
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
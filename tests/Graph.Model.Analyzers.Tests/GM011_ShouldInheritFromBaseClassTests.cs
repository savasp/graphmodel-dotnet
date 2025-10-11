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

using Xunit;


using static Cvoya.Graph.Model.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Cvoya.Graph.Model.Analyzers.GraphModelAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class GM011_ShouldInheritFromBaseClassTests
{
    [Fact]
    public async Task NodeInheritingFromBaseClass_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public record TestNode : Node
            {
                public string Name { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipInheritingFromBaseClass_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public record TestRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                public string CustomProperty { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task AbstractNodeInheritingFromBaseClass_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public abstract record BaseTestNode : Node
            {
                public string Name { get; set; } = string.Empty;
            }
            
            public record ConcreteTestNode : BaseTestNode
            {
                public string Description { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeImplementingInterfaceDirectly_ProducesWarning()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public record {|#0:TestNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM011")
            .WithLocation(0)
            .WithArguments("TestNode", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipImplementingInterfaceDirectly_ProducesWarning()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public record {|#0:TestRelationship|}(string StartNodeId, string EndNodeId) : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public string Type { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM011")
            .WithLocation(0)
            .WithArguments("TestRelationship", "Relationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task AbstractNodeImplementingInterfaceDirectly_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public abstract record AbstractTestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InterfaceExtendingINode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public interface ICustomNode : INode
            {
                string CustomProperty { get; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassImplementingInterfaceDirectly_ProducesWarning()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class {|#0:TestNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public IReadOnlyList<string> Labels { get; init; } = new List<string>();
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM011")
            .WithLocation(0)
            .WithArguments("TestNode", "Node", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CustomBaseClassInheritingFromNode_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public abstract record BaseNode : Node
            {
                public string CommonProperty { get; set; } = string.Empty;
            }
            
            public record TestNode : BaseNode
            {
                public string SpecificProperty { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }
}


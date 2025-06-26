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

public class GM001_MissingParameterlessClassConstructorTests
{
    [Fact]
    public async Task ValidNodeWithParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public TestNode(string id, string name)
                {
                    Id = id;
                    Name = name;
                }
                
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithoutParameterlessConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class {|#0:TestNode|} : INode
            {
                public TestNode(string id)
                {
                    Id = id;
                }
                
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM001")
            .WithLocation(0)
            .WithArguments("TestNode", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithoutParameterlessConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class {|#0:TestRelationship|} : IRelationship
            {
                public TestRelationship(string id)
                {
                    Id = id;
                }
                
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public string Type { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM001")
            .WithLocation(0)
            .WithArguments("TestRelationship", "IRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ValidRelationshipWithParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestRelationship : IRelationship
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
            using Cvoya.Graph.Model;
            
            public class {|#0:TestNode|} : INode
            {
                private TestNode() { }
                
                public TestNode(string id)
                {
                    Id = id;
                }
                
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM001")
            .WithLocation(0)
            .WithArguments("TestNode", "INode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithInternalParameterlessConstructor_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
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
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

public class GM002_PropertyMustHavePublicAccessorsTests
{
    [Fact]
    public async Task ValidNodeWithPublicGettersAndSetters_NoDiagnostic()
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
    public async Task ValidNodeWithPublicGettersAndInitSetters_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithPrivateGetter_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { private get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithPrivateSetter_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { get; private set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithInternalGetter_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { internal get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithInternalSetter_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { get; internal set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithPrivateAccessors_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public string {|#0:Type|} { get; private set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Type", "TestRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithReadOnlyProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { get; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithWriteOnlyProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithMixedAccessLevels_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string {|#0:Name|} { get; private set; } = string.Empty;
                public string {|#1:Description|} { internal get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM002").WithLocation(0).WithArguments("Name", "TestNode"),
            VerifyCS.Diagnostic("GM002").WithLocation(1).WithArguments("Description", "TestNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithPrivateAccessors_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class BaseNode : INode
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public string {|#0:Name|} { get; private set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM002")
            .WithLocation(0)
            .WithArguments("Name", "DerivedNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDiagnostic()
    {
        var test = """
            public class RegularClass
            {
                public string Name { get; private set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }


}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}
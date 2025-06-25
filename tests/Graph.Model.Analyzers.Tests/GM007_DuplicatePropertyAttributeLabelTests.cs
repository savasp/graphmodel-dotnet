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

public class GM007_DuplicatePropertyAttributeLabelTests
{
    [Fact]
    public async Task ValidNodeWithUniquePropertyLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "firstName")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "lastName")]
                public string LastName { get; set; } = string.Empty;
                
                [Property(Label = "age")]
                public int Age { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithDuplicatePropertyLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "name")]
                public string {|#0:LastName|} { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM007")
            .WithLocation(0)
            .WithArguments("LastName", "TestNode", "name", "FirstName", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithDuplicatePropertyLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class BaseNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string {|#0:Name|} { get; set; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                [Property(Label = "name")]
                public string DisplayName { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM007")
            .WithLocation(0)
            .WithArguments("Name", "BaseNode", "name", "DisplayName", "DerivedNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithMultipleDuplicateLabels_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "name")]
                public string {|#0:LastName|} { get; set; } = string.Empty;
                
                [Property(Label = "value")]
                public string Value1 { get; set; } = string.Empty;
                
                [Property(Label = "value")]
                public string {|#1:Value2|} { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM007").WithLocation(0).WithArguments("LastName", "TestNode", "name", "FirstName", "TestNode"),
            VerifyCS.Diagnostic("GM007").WithLocation(1).WithArguments("Value2", "TestNode", "value", "Value1", "TestNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class BaseNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string {|#0:Name|} { get; set; } = string.Empty;
            }
            
            public class MiddleNode : BaseNode
            {
                [Property(Label = "description")]
                public string {|#1:Description|} { get; set; } = string.Empty;
            }
            
            public class DerivedNode : MiddleNode
            {
                [Property(Label = "name")]
                public string DisplayName { get; set; } = string.Empty;
                
                [Property(Label = "description")]
                public string DetailedDescription { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM007").WithLocation(0).WithArguments("Name", "BaseNode", "name", "DisplayName", "DerivedNode"),
            VerifyCS.Diagnostic("GM007").WithLocation(1).WithArguments("Description", "MiddleNode", "description", "DetailedDescription", "DerivedNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithDuplicatePropertyLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                
                [Property(Label = "weight")]
                public int Weight { get; set; }
                
                [Property(Label = "weight")]
                public double {|#0:Strength|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("GM007")
            .WithLocation(0)
            .WithArguments("Strength", "TestRelationship", "weight", "Weight", "TestRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithCaseSensitiveDuplicates_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "Name")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "name")]
                public string LastName { get; set; } = string.Empty;
            }
            """;

        // The analyzer treats property labels as case-sensitive, so "Name" and "name" are different
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithPropertiesWithoutAttributes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string Name { get; set; } = string.Empty;
                
                // Property without attribute should not cause issues
                public string Description { get; set; } = string.Empty;
                
                [Property(Label = "age")]
                public int Age { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SeparateTypesWithSamePropertyLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string Name { get; set; } = string.Empty;
            }
            
            public class CompanyNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string CompanyName { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithEmptyPropertyLabel_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "")]
                public string LastName { get; set; } = string.Empty;
            }
            """;

        // Empty labels might be handled differently - this test assumes they're not checked for duplicates
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class RegularClass
            {
                [Property(Label = "name")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "name")]
                public string LastName { get; set; } = string.Empty;
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
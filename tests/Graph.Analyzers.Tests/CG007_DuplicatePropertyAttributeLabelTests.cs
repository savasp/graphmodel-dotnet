// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG007_DuplicatePropertyAttributeLabelTests
{
    [Fact]
    public async Task ValidNodeWithUniquePropertyLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
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
            using Cvoya.Graph;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string FirstName { get; set; } = string.Empty;
                
                [Property(Label = "name")]
                public string {|#0:LastName|} { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG007")
            .WithLocation(0)
            .WithArguments("LastName", "TestNode", "name", "FirstName", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithDuplicatePropertyLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class BaseNode : Node
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string Name { get; set; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                [Property(Label = "name")]
                public string {|#0:DisplayName|} { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG007")
            .WithLocation(0)
            .WithArguments("DisplayName", "DerivedNode", "name", "Name", "BaseNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithMultipleDuplicateLabels_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
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
            VerifyCS.Diagnostic("CG007").WithLocation(0).WithArguments("LastName", "TestNode", "name", "FirstName", "TestNode"),
            VerifyCS.Diagnostic("CG007").WithLocation(1).WithArguments("Value2", "TestNode", "value", "Value1", "TestNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class BaseNode : Node
            {
                public string Id { get; init; } = string.Empty;
                
                [Property(Label = "name")]
                public string Name { get; set; } = string.Empty;
            }
            
            public class MiddleNode : BaseNode
            {
                [Property(Label = "description")]
                public string Description { get; set; } = string.Empty;
            }
            
            public class DerivedNode : MiddleNode
            {
                [Property(Label = "name")]
                public string {|#0:DisplayName|} { get; set; } = string.Empty;
                
                [Property(Label = "description")]
                public string {|#1:DetailedDescription|} { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG007").WithLocation(0).WithArguments("DisplayName", "DerivedNode", "name", "Name", "BaseNode"),
            VerifyCS.Diagnostic("CG007").WithLocation(1).WithArguments("DetailedDescription", "DerivedNode", "description", "Description", "MiddleNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithDuplicatePropertyLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                
                [Property(Label = "weight")]
                public int Weight { get; set; }
                
                [Property(Label = "weight")]
                public double {|#0:Strength|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG007")
            .WithLocation(0)
            .WithArguments("Strength", "TestRelationship", "weight", "Weight", "TestRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithCaseSensitiveDuplicates_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class TestNode : Node
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
            using Cvoya.Graph;
            
            public class TestNode : Node
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
            using Cvoya.Graph;
            
            public class PersonNode : Node
            {
                [Property(Label = "name")]
                public string Name { get; set; } = string.Empty;
            }
            
            public class CompanyNode : Node
            {
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
            using Cvoya.Graph;
            
            public class TestNode : Node
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
            using Cvoya.Graph;
            
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
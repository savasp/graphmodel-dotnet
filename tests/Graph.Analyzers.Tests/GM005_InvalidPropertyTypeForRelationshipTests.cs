// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG005_InvalidPropertyTypeForRelationshipTests
{
    [Fact]
    public async Task ValidRelationshipWithSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public string Type { get; set; } = string.Empty;
                public int Weight { get; set; }
                public bool IsActive { get; set; }
                public DateTime CreatedAt { get; set; }
                public Guid UniqueId { get; set; }
                public decimal Cost { get; set; }
                public double Strength { get; set; }
                public float Confidence { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidRelationshipWithCollectionsOfSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public List<string> Tags { get; set; } = new();
                public string[] Categories { get; set; } = Array.Empty<string>();
                public HashSet<int> Scores { get; set; } = new();
                public IEnumerable<bool> Flags { get; set; } = Enumerable.Empty<bool>();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidRelationshipWithEnums_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public enum Priority
            {
                Low,
                Medium,
                High
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Priority CurrentPriority { get; set; }
                public List<Priority> PriorityHistory { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Address {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Location", "TestRelationship", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithCollectionOfComplexTypes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class Metadata
            {
                public string Key { get; set; } = string.Empty;
                public string Value { get; set; } = string.Empty;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public List<Metadata> {|#0:MetadataList|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("MetadataList", "TestRelationship", "List<Metadata>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithUnsupportedType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Threading.Tasks;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Task {|#0:AsyncOperation|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("AsyncOperation", "TestRelationship", "Task");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithDelegate_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Action {|#0:Callback|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Callback", "TestRelationship", "Action");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithNullableTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public int? Weight { get; set; }
                public DateTime? CreatedAt { get; set; }
                public bool? IsActive { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithMultipleInvalidProperties_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            using System.Threading.Tasks;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Address {|#0:Location|} { get; set; } = new();
                public Task {|#1:Operation|} { get; set; } = null!;
                public Action {|#2:Callback|} { get; set; } = null!;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG005").WithLocation(0).WithArguments("Location", "TestRelationship", "Address"),
            VerifyCS.Diagnostic("CG005").WithLocation(1).WithArguments("Operation", "TestRelationship", "Task"),
            VerifyCS.Diagnostic("CG005").WithLocation(2).WithArguments("Callback", "TestRelationship", "Action")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeClassIgnored_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
            }
            
            public class TestNode : Node
            {
                // This would be valid for INode but CG005 only applies to IRelationship
                public Address Location { get; set; } = new();
            }
            """;

        // This test ensures CG005 only applies to IRelationship implementations
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InheritedRelationshipWithInvalidProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
            }
            
            public class BaseRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            public class DerivedRelationship : BaseRelationship
            {
                public Address {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Location", "DerivedRelationship", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithArrayOfComplexTypes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class Tag
            {
                public string Name { get; set; } = string.Empty;
                public string Value { get; set; } = string.Empty;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Tag[] {|#0:Tags|} { get; set; } = Array.Empty<Tag>();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Tags", "TestRelationship", "Tag[]");

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}
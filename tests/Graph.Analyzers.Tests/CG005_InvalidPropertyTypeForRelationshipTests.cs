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

    [Theory]
    [InlineData("System.Collections.Generic.Dictionary<string, string>", "Dictionary<String, String>")]
    [InlineData("System.Collections.IDictionary", "IDictionary")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>", "List<Dictionary<String, String>>")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.List<System.Threading.Tasks.Task>>", "List<List<Task>>")]
    [InlineData("System.IO.Stream", "Stream")]
    [InlineData("System.Net.IPAddress", "IPAddress")]
    [InlineData("System.Reflection.MemberInfo", "MemberInfo")]
    [InlineData("System.Runtime.InteropServices.GCHandle", "GCHandle")]
    public async Task RelationshipWithUnsupportedShape_ProducesDiagnostic(string propertyType, string reportedType)
    {
        var test = $$"""
            using Cvoya.Graph;

            public class TestRelationship : Relationship
            {
                public {{propertyType}} {|#0:Unsupported|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Unsupported", "TestRelationship", reportedType);

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithNonSerializedUnsupportedProperties_ProducesNoDiagnostics()
    {
        // Ignored properties, static properties, and indexers are never serialized, so an
        // unsupported type on one of them is not a model error.
        var test = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public sealed class TestRelationship : Relationship
            {
                [Property(Ignore = true)]
                public Task Ignored { get; set; } = null!;

                public static Task Shared { get; set; } = Task.CompletedTask;

                public Task this[int index]
                {
                    get => Task.CompletedTask;
                    set { }
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithNonConstructibleCollection_ProducesDiagnostic()
    {
        // Queue<int> is a collection of simple values the serializer cannot construct (only arrays,
        // List<T>/list interfaces, and HashSet<T>/set interfaces are supported), so CG005 fires
        // rather than letting generated source fail to compile (#362).
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public Queue<int> {|#0:Pending|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Pending", "TestRelationship", "Queue<Int32>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithPointType_NoDiagnostic()
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
                public Point Location { get; set; } = new();
                public Point? MaybeLocation { get; set; }
                public List<Point> Waypoints { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithSystemDrawingPointType_ProducesDiagnostics()
    {
        // Mirrors NodeWithSystemDrawingPointType_ProducesDiagnostics: System.Drawing.Point is not a
        // supported simple type anywhere in the stack, and relationships accept only simple values
        // and collections of them (#387).
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public System.Drawing.Point {|#0:Location|} { get; set; }
                public System.Drawing.Point? {|#1:MaybeLocation|} { get; set; }
                public List<System.Drawing.Point> {|#2:Locations|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG005").WithLocation(0).WithArguments("Location", "TestRelationship", "System.Drawing.Point"),
            VerifyCS.Diagnostic("CG005").WithLocation(1).WithArguments("MaybeLocation", "TestRelationship", "System.Drawing.Point?"),
            VerifyCS.Diagnostic("CG005").WithLocation(2).WithArguments("Locations", "TestRelationship", "List<System.Drawing.Point>"),
        };

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

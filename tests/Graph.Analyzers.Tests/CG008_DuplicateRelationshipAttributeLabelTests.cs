// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG008_DuplicateRelationshipAttributeLabelTests
{
    [Fact]
    public async Task ValidRelationshipWithUniqueLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public record FollowsRelationship : Relationship;
            
            [Relationship("LIKES")]
            public record LikesRelationship : Relationship;
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithDuplicateLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public record FollowsRelationship : Relationship;
            
            [Relationship("FOLLOWS")]
            public record {|#0:DuplicateFollowsRelationship|} : Relationship;
            """;

        var expected = VerifyCS.Diagnostic("CG008")
            .WithLocation(0)
            .WithArguments("DuplicateFollowsRelationship", "FOLLOWS", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedRelationshipWithDuplicateLabel_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public record BaseFollowsRelationship : Relationship;
            
            [Relationship("FOLLOWS")]
            public record {|#0:DerivedFollowsRelationship|} : BaseFollowsRelationship
            {
                public string CustomType { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG008")
            .WithLocation(0)
            .WithArguments("DerivedFollowsRelationship", "FOLLOWS", "BaseFollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipInheritingLabelFromParent_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public class BaseFollowsRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            // This relationship inherits the label from parent without specifying its own
            public class DerivedFollowsRelationship : BaseFollowsRelationship
            {
                public string Type { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithCaseSensitiveDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            [Relationship("follows")]
            public class {|#0:LowercaseFollowsRelationship|} : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            """;

        // Assuming the analyzer treats relationship labels as case-insensitive
        var expected = VerifyCS.Diagnostic("CG008")
            .WithLocation(0)
            .WithArguments("LowercaseFollowsRelationship", "follows", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("SOCIAL")]
            public class BaseSocialRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : BaseSocialRelationship
            {
                public string Type { get; set; } = string.Empty;
            }
            
            [Relationship("SOCIAL")]
            public class {|#0:DuplicateSocialRelationship|} : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG008")
            .WithLocation(0)
            .WithArguments("DuplicateSocialRelationship", "SOCIAL", "BaseSocialRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithoutAttribute_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            // Relationship without attribute should not cause conflicts
            public class GenericRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleRelationshipsWithSameLabel_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship1 : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            [Relationship("FOLLOWS")]
            public class {|#0:FollowsRelationship2|} : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            [Relationship("FOLLOWS")]
            public class {|#1:FollowsRelationship3|} : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG008").WithLocation(0).WithArguments("FollowsRelationship2", "FOLLOWS", "FollowsRelationship1"),
            VerifyCS.Diagnostic("CG008").WithLocation(1).WithArguments("FollowsRelationship3", "FOLLOWS", "FollowsRelationship1")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithEmptyLabel_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Relationship("")]
            public class EmptyLabelRelationship1 : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            
            [Relationship("")]
            public class EmptyLabelRelationship2 : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
            }
            """;

        // Empty labels might be handled differently - this test assumes they're not checked for duplicates
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDuplicateLabelDiagnostic()
    {
        // CG008 only compares labels across types that implement IRelationship - neither of these
        // classes does, so CG008 stays silent (each still gets a CG012 for the misapplied
        // attribute, which is out of scope for this test class and covered by
        // CG012_MisappliedNodeOrRelationshipAttributeTests instead).
        var test = """
            using Cvoya.Graph;

            [{|#0:Relationship("FOLLOWS")|}]
            public class RegularClass1
            {
                public string Name { get; set; } = string.Empty;
            }

            [{|#1:Relationship("FOLLOWS")|}]
            public class RegularClass2
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCG012.Diagnostic("CG012").WithLocation(0).WithArguments("RegularClass1", "Relationship", "IRelationship"),
            VerifyCG012.Diagnostic("CG012").WithLocation(1).WithArguments("RegularClass2", "Relationship", "IRelationship"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedef for the CG012 diagnostics incidentally produced by the misapplied-attribute
// fixtures above (this class's own diagnostics use the file-scoped VerifyCS below).
file static class VerifyCG012
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}

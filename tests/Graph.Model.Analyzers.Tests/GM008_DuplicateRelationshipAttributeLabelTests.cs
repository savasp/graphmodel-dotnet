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

public class GM008_DuplicateRelationshipAttributeLabelTests
{
    [Fact]
    public async Task ValidRelationshipWithUniqueLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("LIKES")]
            public class LikesRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithDuplicateLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class {|#0:DuplicateFollowsRelationship|} : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM008")
            .WithLocation(0)
            .WithArguments("DuplicateFollowsRelationship", "FOLLOWS", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedRelationshipWithDuplicateLabel_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class BaseFollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class {|#0:DerivedFollowsRelationship|} : BaseFollowsRelationship
            {
                public string Type { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM008")
            .WithLocation(0)
            .WithArguments("DerivedFollowsRelationship", "FOLLOWS", "BaseFollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipInheritingLabelFromParent_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class BaseFollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
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
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("follows")]
            public class {|#0:LowercaseFollowsRelationship|} : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        // Assuming the analyzer treats relationship labels as case-insensitive
        var expected = VerifyCS.Diagnostic("GM008")
            .WithLocation(0)
            .WithArguments("LowercaseFollowsRelationship", "follows", "FollowsRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("SOCIAL")]
            public class BaseSocialRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : BaseSocialRelationship
            {
                public string Type { get; set; } = string.Empty;
            }
            
            [Relationship("SOCIAL")]
            public class {|#0:DuplicateSocialRelationship|} : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM008")
            .WithLocation(0)
            .WithArguments("DuplicateSocialRelationship", "SOCIAL", "BaseSocialRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithoutAttribute_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            // Relationship without attribute should not cause conflicts
            public class GenericRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleRelationshipsWithSameLabel_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("FOLLOWS")]
            public class FollowsRelationship1 : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class {|#0:FollowsRelationship2|} : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class {|#1:FollowsRelationship3|} : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM008").WithLocation(0).WithArguments("FollowsRelationship2", "FOLLOWS", "FollowsRelationship1"),
            VerifyCS.Diagnostic("GM008").WithLocation(1).WithArguments("FollowsRelationship3", "FOLLOWS", "FollowsRelationship1")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithEmptyLabel_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using Cvoya.Graph.Model;
            
            [Relationship("")]
            public class EmptyLabelRelationship1 : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
            }
            
            [Relationship("")]
            public class EmptyLabelRelationship2 : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
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
            
            [Relationship("FOLLOWS")]
            public class RegularClass1
            {
                public string Name { get; set; } = string.Empty;
            }
            
            [Relationship("FOLLOWS")]
            public class RegularClass2
            {
                public string Name { get; set; } = string.Empty;
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
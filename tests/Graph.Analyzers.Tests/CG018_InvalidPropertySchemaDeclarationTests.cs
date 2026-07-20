// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG018_InvalidPropertySchemaDeclarationTests
{
    [Fact]
    public async Task NullableReferenceKeyOnNode_ProducesDiagnostic()
    {
        const string test = """
            #nullable enable
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public string? {|#0:CustomerNumber|} { get; set; }
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("CustomerNumber", "Customer", "IsKey = true requires a non-nullable property");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NullableValueKeyOnRelationship_ProducesDiagnostic()
    {
        const string test = """
            #nullable enable
            using Cvoya.Graph;

            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                [Property(IsKey = true)]
                public int? {|#0:Sequence|} { get; set; }
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("Sequence", "Knows", "IsKey = true requires a non-nullable property");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task SimpleCollectionKey_ProducesDiagnostic()
    {
        const string test = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public List<string> {|#0:Aliases|} { get; set; } = [];
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments(
                "Aliases",
                "Customer",
                "IsKey = true requires a graph-storable scalar; 'List<String>' is not a scalar");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexKeyOnNode_ProducesDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record Address
            {
                public string City { get; set; } = string.Empty;
            }

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public Address {|#0:Address|} { get; set; } = new();
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments(
                "Address",
                "Customer",
                "IsKey = true requires a graph-storable scalar; 'Address' is not a scalar");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexCollectionKey_ProducesDiagnostic()
    {
        const string test = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string City { get; set; } = string.Empty;
            }

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public List<Address> {|#0:Addresses|} { get; set; } = [];
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments(
                "Addresses",
                "Customer",
                "IsKey = true requires a graph-storable scalar; 'List<Address>' is not a scalar");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task IgnoredKeyAndSchemaFlags_ProducesOneDiagnosticNamingEveryConflict()
    {
        const string test = """
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(Ignore = true, IsKey = true, IsUnique = true, IsIndexed = true, IsRequired = true)]
                public string {|#0:ImportMarker|} { get; set; } = string.Empty;
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments(
                "ImportMarker",
                "Customer",
                "Ignore = true cannot be combined with IsKey = true, IsUnique = true, IsIndexed = true, IsRequired = true");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Theory]
    [InlineData("IsUnique", "IsUnique = true")]
    [InlineData("IsIndexed", "IsIndexed = true")]
    [InlineData("IsRequired", "IsRequired = true")]
    public async Task IgnoredSchemaBehaviorWithoutKey_ProducesDiagnostic(string flag, string configuredFlag)
    {
        var test = $$"""
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(Ignore = true, {{flag}} = true)]
                public string {|#0:ImportMarker|} { get; set; } = string.Empty;
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments(
                "ImportMarker",
                "Customer",
                $"Ignore = true cannot be combined with {configuredFlag}");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InvalidInheritedKey_ProducesOneDiagnosticAtBaseDeclaration()
    {
        const string test = """
            #nullable enable
            using Cvoya.Graph;

            public abstract record CustomerBase : Node
            {
                [Property(IsKey = true)]
                public string? {|#0:CustomerNumber|} { get; set; }
            }

            public sealed record Customer : CustomerBase;
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("CustomerNumber", "CustomerBase", "IsKey = true requires a non-nullable property");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task UnsupportedPropertyType_KeepsExistingTypeDiagnosticWithoutDuplicate()
    {
        const string test = """
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public object {|#0:Payload|} { get; set; } = new();
            }
            """;

        var expected = Diagnostic("CG004").WithLocation(0)
            .WithArguments("Payload", "Customer", "Object");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexRelationshipProperty_KeepsExistingTypeDiagnosticWithoutDuplicate()
    {
        const string test = """
            using Cvoya.Graph;

            public record Address
            {
                public string City { get; set; } = string.Empty;
            }

            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                [Property(IsKey = true)]
                public Address {|#0:Address|} { get; set; } = new();
            }
            """;

        var expected = Diagnostic("CG005").WithLocation(0)
            .WithArguments("Address", "Knows", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task KeylessNodeAndRelationship_ProduceNoDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record Customer : Node
            {
                public string Name { get; set; } = string.Empty;
            }

            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                public int Since { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SupportedScalarKeysAndCompositeKeys_ProduceNoDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record Customer : Node
            {
                [Property(IsKey = true)]
                public string Tenant { get; set; } = string.Empty;

                [Property(IsKey = true)]
                public long CustomerNumber { get; set; }
            }

            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                [Property(IsKey = true)]
                public int Sequence { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidInheritedKey_ProducesNoDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public abstract record CustomerBase : Node
            {
                [Property(IsKey = true)]
                public string Tenant { get; set; } = string.Empty;
            }

            public sealed record Customer : CustomerBase;
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PlainIdPropertyWithoutOptIn_ProducesNoDiagnostic()
    {
        const string test = """
            using Cvoya.Graph;

            public record Customer : Node
            {
                public new string Id { get; init; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task LookalikePropertyAttribute_ProducesNoDiagnostic()
    {
        const string test = """
            #nullable enable
            using System;
            using Cvoya.Graph;

            namespace Lookalike
            {
                [AttributeUsage(AttributeTargets.Property)]
                public sealed class PropertyAttribute : Attribute
                {
                    public bool IsKey { get; set; }
                }
            }

            public record Customer : Node
            {
                [Lookalike.Property(IsKey = true)]
                public string? CustomerNumber { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    private static DiagnosticResult Error() =>
        new("CG018", DiagnosticSeverity.Error);
}

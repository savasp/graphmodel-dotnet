// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG015_IneffectiveComplexPropertyAttributeTests
{
    public static TheoryData<string, string, string> SimpleCases => new()
    {
        { "string", "string.Empty", "String" },
        { "int", "0", "Int32" },
        { "int?", "null", "Nullable<Int32>" },
        { "string[]", "[]", "String[]" },
        { "List<int>", "[]", "List<Int32>" },
    };

    [Theory]
    [MemberData(nameof(SimpleCases))]
    public async Task SimpleOrSimpleCollectionProperty_ProducesDiagnostic(
        string propertyType,
        string initializer,
        string shortTypeName)
    {
        var test = $$"""
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record TestNode : Node
            {
                [{|#0:ComplexProperty|}]
                public {{propertyType}} Value { get; set; } = {{initializer}};
            }
            """;

        var expected = Warning()
            .WithLocation(0)
            .WithArguments("Value", $"property type '{shortTypeName}' is simple or a simple collection");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespaceRelationshipType_ProducesDiagnostic(string relationshipType)
    {
        var test = $$"""
            using Cvoya.Graph;

            public record Address
            {
                public string City { get; set; } = string.Empty;
            }

            public record TestNode : Node
            {
                [{|#0:ComplexProperty(RelationshipType = "{{relationshipType}}")|}]
                public Address Value { get; set; } = new();
            }
            """;

        var expected = Warning()
            .WithLocation(0)
            .WithArguments(
                "Value",
                "RelationshipType is empty or whitespace, so convention-based naming is used");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task GenuineComplexProperties_DoNotProduceDiagnostic()
    {
        const string test = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string City { get; set; } = string.Empty;
            }

            public record TestNode : Node
            {
                [ComplexProperty]
                public Address Address { get; set; } = new();

                [ComplexProperty(RelationshipType = "LIVES_AT")]
                public List<Address> Addresses { get; set; } = [];
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    private static DiagnosticResult Warning() =>
        new("CG015", DiagnosticSeverity.Warning);
}

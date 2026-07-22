// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class NullableComplexCollectionElementTests
{
    [Fact]
    public async Task NodeWithNullableComplexElements_ProducesNoDiagnostic()
    {
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address;

            public record Person : Node
            {
                public List<Address?> Addresses { get; set; } = new();
                public Address?[] PreviousAddresses { get; set; } = [];
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithNullableComplexElements_StillReportsRelationshipShape()
    {
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address;

            public record Knows : Relationship
            {
                public List<Address?> {|#0:Addresses|} { get; set; } = new();
            }
            """;

        var expected = Diagnostic("CG005")
            .WithLocation(0)
            .WithArguments("Addresses", "Knows", "List<Address?>");
        await VerifyAnalyzerAsync(test, expected);
    }
}

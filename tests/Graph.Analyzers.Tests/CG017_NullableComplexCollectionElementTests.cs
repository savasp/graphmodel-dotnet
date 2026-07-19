// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG017_NullableComplexCollectionElementTests
{
    [Fact]
    public async Task NodeWithNullableComplexListElement_ProducesDiagnostic()
    {
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public record Person : Node
            {
                public List<Address?> {|#0:Addresses|} { get; set; } = new();
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("Addresses", "Person", "List<Address?>", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableComplexArrayElement_ProducesDiagnostic()
    {
        const string test = """
            #nullable enable
            using Cvoya.Graph;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public record Person : Node
            {
                public Address?[] {|#0:Addresses|} { get; set; } = [];
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("Addresses", "Person", "Address?[]", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableComplexStructElement_ProducesDiagnostic()
    {
        // A nullable value-type element is Nullable<Address>, a distinct shape from an annotated
        // reference type, and equally unrepresentable in the wire model. The message spells it
        // 'Nullable<Address>' because GetShortTypeName only abbreviates nullable reference types.
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public struct Address
            {
                public string Street { get; set; }
            }

            public record Person : Node
            {
                public List<Address?> {|#0:Addresses|} { get; set; } = new();
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("Addresses", "Person", "List<Nullable<Address>>", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedComplexTypeWithNullableComplexElement_ProducesDiagnostic()
    {
        // Generated serialization walks into complex property types, so a nullable element one
        // level down is just as unrepresentable as one declared on the entity itself.
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Tag
            {
                public string Name { get; set; } = string.Empty;
            }

            public record Address
            {
                public List<Tag?> {|#0:Tags|} { get; set; } = new();
            }

            public record Person : Node
            {
                public Address HomeAddress { get; set; } = new();
            }
            """;

        var expected = Error().WithLocation(0)
            .WithArguments("Tags", "Address", "List<Tag?>", "Tag");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithNullableComplexListElement_ProducesDiagnostic()
    {
        // CG005 also fires: relationships reject complex collections outright. CG017 still reports
        // the element nullability so the message names the shape that has to change.
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
            {
                public List<Address?> {|#0:Addresses|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            Diagnostic("CG005").WithLocation(0).WithArguments("Addresses", "Knows", "List<Address?>"),
            Error().WithLocation(0).WithArguments("Addresses", "Knows", "List<Address?>", "Address"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNonNullableComplexListElement_ProducesNoDiagnostic()
    {
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public record Person : Node
            {
                public List<Address> Addresses { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithNullableSimpleCollectionElement_ProducesNoDiagnostic()
    {
        // Simple collections carry a null slot on the wire (#406/#420), so they stay supported.
        const string test = """
            #nullable enable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Person : Node
            {
                public List<string?> Names { get; set; } = new();
                public List<int?> Scores { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NullableObliviousComplexListElement_ProducesNoDiagnostic()
    {
        // Without a nullable context the declaration carries no annotation to act on; treating
        // oblivious as nullable would break every pre-nullable model.
        const string test = """
            #nullable disable
            using System.Collections.Generic;
            using Cvoya.Graph;

            public record Address
            {
                public string Street { get; set; }
            }

            public record Person : Node
            {
                public List<Address> Addresses { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    private static DiagnosticResult Error() =>
        new("CG017", DiagnosticSeverity.Error);
}

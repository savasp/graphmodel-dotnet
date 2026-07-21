// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG006_ComplexTypeContainsGraphInterfaceTypesTests
{
    [Fact]
    public async Task ValidComplexTypeWithSimpleProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
                public int ZipCode { get; set; }
                public bool IsActive { get; set; }
                public DateTime CreatedAt { get; set; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNestedComplexTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class ContactInfo
            {
                public string Email { get; set; } = string.Empty;
                public string Phone { get; set; } = string.Empty;
            }
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
                public ContactInfo Contact { get; set; } = new();
                public List<ContactInfo> AlternativeContacts { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeWithINodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode OwnerNode { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithIRelationshipProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidMetadata
            {
                public string Key { get; set; } = string.Empty;
                public string Value { get; set; } = string.Empty;
                public IRelationship Connection { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidMetadata {|#0:Metadata|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidMetadata", "Metadata", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithConcreteNodeProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public PersonNode Owner { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithListOfNodes_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class InvalidContainer
            {
                public string Name { get; set; } = string.Empty;
                public List<INode> Nodes { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidContainer {|#0:Container|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidContainer", "Container", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedComplexTypeWithGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InnerType
            {
                public string Value { get; set; } = string.Empty;
                public INode Node { get; set; } = null!;
            }
            
            public class OuterType
            {
                public string Name { get; set; } = string.Empty;
                public InnerType Inner { get; set; } = new();
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public OuterType {|#0:Data|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("OuterType", "Data", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MultipleComplexTypesWithGraphInterfaces_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode OwnerNode { get; set; } = null!;
            }
            
            public class InvalidMetadata
            {
                public string Key { get; set; } = string.Empty;
                public IRelationship Connection { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
                public InvalidMetadata {|#1:Metadata|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG006").WithLocation(0).WithArguments("InvalidAddress", "Location", "TestNode"),
            VerifyCS.Diagnostic("CG006").WithLocation(1).WithArguments("InvalidMetadata", "Metadata", "TestNode")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithNullableGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode? OwnerNode { get; set; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithInvalidComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidMetadata
            {
                public string Key { get; set; } = string.Empty;
                public INode RelatedNode { get; set; } = null!;
            }
            
            public class TestRelationship : Relationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                public InvalidMetadata {|#0:Metadata|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidMetadata", "Metadata", "TestRelationship");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CollectionOfComplexTypesWithGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class InvalidItem
            {
                public string Name { get; set; } = string.Empty;
                public INode RelatedNode { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<InvalidItem> {|#0:Items|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidItem", "Items", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ArrayOfComplexTypesWithGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidTag
            {
                public string Name { get; set; } = string.Empty;
                public IRelationship Connection { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidTag[] {|#0:Tags|} { get; set; } = Array.Empty<InvalidTag>();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidTag", "Tags", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NonSerializedInvalidComplexProperties_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public sealed class InvalidHolder
            {
                public INode Parent { get; set; } = null!;
            }

            public sealed class TestNode : Node
            {
                [Property(Ignore = true)]
                public InvalidHolder Ignored { get; set; } = new();

                public static InvalidHolder Shared { get; set; } = new();

                public InvalidHolder this[int index]
                {
                    get => new();
                    set { }
                }

                private InvalidHolder Hidden { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task EffectiveSerializedMemberParityFixture_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public sealed class EffectiveSerializedMemberParityDetails
            {
                public string Name { get; set; } = string.Empty;

                [Property(Ignore = true)]
                public INode IgnoredNode { get; set; } = null!;

                public static IRelationship SharedRelationship { get; set; } = null!;

                public INode this[int index]
                {
                    get => null!;
                    set { }
                }

                private IRelationship HiddenRelationship { get; set; } = null!;
            }

            [Node("EffectiveSerializedMemberParity")]
            public sealed record EffectiveSerializedMemberParityNode : Node
            {
                public EffectiveSerializedMemberParityDetails Details { get; init; } = new();

                [Property(Ignore = true)]
                public IRelationship IgnoredRelationship { get; init; } = null!;

                public static INode SharedNode { get; set; } = null!;

                public IRelationship this[int index]
                {
                    get => null!;
                    set { }
                }

                private INode HiddenNode { get; set; } = null!;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InheritedSerializedInvalidComplexProperty_ProducesDiagnosticForEachEntity()
    {
        var test = """
            using Cvoya.Graph;

            public sealed class InvalidHolder
            {
                public INode Parent { get; set; } = null!;
            }

            public abstract class BaseNode : Node
            {
                public InvalidHolder {|#0:Holder|} { get; set; } = new();
            }

            public sealed class DerivedNode : BaseNode
            {
            }
            """;

        await VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic("CG006").WithLocation(0).WithArguments("InvalidHolder", "Holder", "BaseNode"),
            VerifyCS.Diagnostic("CG006").WithLocation(0).WithArguments("InvalidHolder", "Holder", "DerivedNode"));
    }

    [Fact]
    public async Task InheritedNodeWithInvalidComplexType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode OwnerNode { get; set; } = null!;
            }
            
            public class BaseNode : Node
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "DerivedNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDiagnostic()
    {
        var test = """
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                // This would be invalid but the class doesn't implement INode/IRelationship
                public object SomeReference { get; set; } = null!;
            }
            
            public class RegularClass
            {
                public InvalidAddress Location { get; set; } = new();
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

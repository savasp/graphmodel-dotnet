// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

/// <summary>
/// Proves the generator discovers dependency serializers for complex properties a concrete entity
/// inherits from an abstract base (see #371). Before the fix, dependency discovery walked only the
/// members declared directly on the concrete type, so an inherited complex value's serializer was
/// never generated/registered and the entity round-trip failed at runtime with "No serializer found".
/// </summary>
public class InheritedComplexPropertyTests
{
    [Fact]
    public void NullableComplexPropertyInheritedFromAbstractBase_RoundTrips()
    {
        const string source = """
            using Cvoya.Graph;

            namespace InheritedNullable;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public abstract record PersonBase : Node
            {
                public Address? Home { get; set; }
            }

            [Node("Person")]
            public record Person : PersonBase
            {
                public string Name { get; set; } = string.Empty;
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        // The core of #371: the inherited complex value's serializer is generated even though `Home`
        // is declared on the abstract base and `Address` is a property type nowhere on `Person` itself.
        Assert.NotNull(assembly.GetType("InheritedNullable.Generated.AddressSerializer", throwOnError: true));
        // The abstract base is never itself a generator root.
        Assert.Null(assembly.GetType("InheritedNullable.Generated.PersonBaseSerializer", throwOnError: false));

        var nodeType = assembly.GetType("InheritedNullable.Person", throwOnError: true)!;
        var addressType = assembly.GetType("InheritedNullable.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "InheritedNullable.Generated.PersonSerializer");

        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Main St");
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Name")!.SetValue(node, "Ada");
        nodeType.GetProperty("Home")!.SetValue(node, address);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var rtHome = nodeType.GetProperty("Home")!.GetValue(roundTripped)!;
        Assert.Equal("Main St", addressType.GetProperty("Street")!.GetValue(rtHome));
    }

    [Fact]
    public void CollectionOfComplexInheritedFromAbstractBase_RoundTrips()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace InheritedCollection;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public abstract record PersonBase : Node
            {
                public List<Address> PreviousAddresses { get; set; } = new();
            }

            [Node("Person")]
            public record Person : PersonBase
            {
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        Assert.NotNull(assembly.GetType("InheritedCollection.Generated.AddressSerializer", throwOnError: true));

        var nodeType = assembly.GetType("InheritedCollection.Person", throwOnError: true)!;
        var addressType = assembly.GetType("InheritedCollection.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "InheritedCollection.Generated.PersonSerializer");

        var addresses = (System.Collections.IList)typeof(List<>).MakeGenericType(addressType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Elm");
        addresses.Add(address);
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("PreviousAddresses")!.SetValue(node, addresses);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var rtAddresses = (System.Collections.IList)nodeType.GetProperty("PreviousAddresses")!.GetValue(roundTripped)!;
        Assert.Single(rtAddresses);
        Assert.Equal("Elm", addressType.GetProperty("Street")!.GetValue(rtAddresses[0]!));
    }

    [Fact]
    public void ComplexPropertyInheritedByRelationship_RoundTrips()
    {
        // The dependency-discovery path is entity-kind agnostic, so a relationship that inherits a
        // complex property gets the same fix.
        const string source = """
            using Cvoya.Graph;

            namespace InheritedRelationship;

            public record Metadata
            {
                public string Note { get; set; } = string.Empty;
            }

            public abstract record LinkBase : Relationship
            {
                public Metadata? Info { get; set; }
            }

            [Relationship("LINKS")]
            public record Links : LinkBase;
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        Assert.NotNull(assembly.GetType("InheritedRelationship.Generated.MetadataSerializer", throwOnError: true));

        var relType = assembly.GetType("InheritedRelationship.Links", throwOnError: true)!;
        var metadataType = assembly.GetType("InheritedRelationship.Metadata", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "InheritedRelationship.Generated.LinksSerializer");

        var metadata = Activator.CreateInstance(metadataType)!;
        metadataType.GetProperty("Note")!.SetValue(metadata, "hi");
        var rel = Activator.CreateInstance(relType)!;
        relType.GetProperty("Info")!.SetValue(rel, metadata);

        var roundTripped = serializer.Deserialize(serializer.Serialize(rel));

        var rtInfo = relType.GetProperty("Info")!.GetValue(roundTripped)!;
        Assert.Equal("hi", metadataType.GetProperty("Note")!.GetValue(rtInfo));
    }

    [Fact]
    public void InheritedAndDeclaredPropertiesOfSameComplexType_EmitSerializerOnce()
    {
        // Discovery deduplicates dependency identities: an inherited `Home` and a declared `Work`,
        // both `Address`, must not produce two AddressSerializer definitions or registrations.
        const string source = """
            using Cvoya.Graph;

            namespace DedupedComplex;

            public record Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public abstract record PersonBase : Node
            {
                public Address? Home { get; set; }
            }

            [Node("Person")]
            public record Person : PersonBase
            {
                public Address? Work { get; set; }
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);

        Assert.Equal(1, CountOccurrences(generated, "internal sealed class AddressSerializer"));
        Assert.Equal(
            1,
            CountOccurrences(generated, "EntitySerializerRegistry.Instance.Register(typeof(DedupedComplex.Address)"));
        // A compile-errors section would indicate malformed/duplicate generated output.
        Assert.DoesNotContain("== Compile errors in output compilation ==", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void HiddenBaseComplexProperty_UsesMostDerivedPropertyOnly()
    {
        const string source = """
            using Cvoya.Graph;

            namespace HiddenComplex;

            public record Details
            {
                public string Note { get; set; } = string.Empty;
            }

            public abstract record PersonBase : Node
            {
                public Details Value { get; set; } = new();
            }

            [Node("Person")]
            public record Person : PersonBase
            {
                public new string Value { get; set; } = string.Empty;
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);

        // The most-derived declaration is the effective serialized property. The hidden base
        // declaration must neither emit a second serialization block nor pull its complex type into
        // the dependency catalog.
        Assert.Null(assembly.GetType("HiddenComplex.Generated.DetailsSerializer", throwOnError: false));

        var nodeType = assembly.GetType("HiddenComplex.Person", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "HiddenComplex.Generated.PersonSerializer");
        var node = Activator.CreateInstance(nodeType)!;
        var derivedValueProperty = nodeType.GetProperties()
            .Single(property => property.Name == "Value" && property.DeclaringType == nodeType);
        derivedValueProperty.SetValue(node, "derived");

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        Assert.Equal("derived", derivedValueProperty.GetValue(roundTripped));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal);
            index >= 0;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }
}

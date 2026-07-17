// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen.Tests;

using Cvoya.Graph.Serialization;

/// <summary>
/// Compiles and executes generated serializers for struct complex-property values (see #363). Before
/// the fix the analyzer accepted a nested struct but the generator discovered serializers only for
/// classes, so the emitted schema referenced an <c>AddressSerializer</c> that was never produced.
/// These tests prove the struct serializer is generated, registered, round-trips, and exposes a
/// nested schema.
/// </summary>
public class StructComplexPropertyTests
{
    [Fact]
    public void SettableStructComplexProperty_RoundTrips()
    {
        const string source = """
            using Cvoya.Graph;

            namespace SettableStruct;

            public struct Address
            {
                public string Street { get; set; }
                public int Unit { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public string Name { get; set; } = string.Empty;
                public Address HomeAddress { get; set; }
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("SettableStruct.Person", throwOnError: true)!;
        var addressType = assembly.GetType("SettableStruct.Address", throwOnError: true)!;
        // The whole point of #363: a serializer is generated for the struct value type.
        Assert.NotNull(assembly.GetType("SettableStruct.Generated.AddressSerializer", throwOnError: true));
        var serializer = CreateSerializer(assembly, "SettableStruct.Generated.PersonSerializer");

        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Main St");
        addressType.GetProperty("Unit")!.SetValue(address, 12);
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Name")!.SetValue(node, "Ada");
        nodeType.GetProperty("HomeAddress")!.SetValue(node, address);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var rtAddress = nodeType.GetProperty("HomeAddress")!.GetValue(roundTripped)!;
        Assert.Equal(addressType, rtAddress.GetType());
        Assert.Equal("Main St", addressType.GetProperty("Street")!.GetValue(rtAddress));
        Assert.Equal(12, addressType.GetProperty("Unit")!.GetValue(rtAddress));
    }

    [Fact]
    public void InitOnlyStructComplexProperty_RoundTrips()
    {
        // Mirrors CG014_...StructUsedAsComplexPropertyTypeOnValidClassNode_NoDiagnostic: an init-only
        // struct is constructed via `new Address() { Street = ..., Unit = ... }`.
        const string source = """
            using Cvoya.Graph;

            namespace InitOnlyStruct;

            public struct Address
            {
                public string Street { get; init; }
                public int Unit { get; init; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("InitOnlyStruct.Person", throwOnError: true)!;
        var addressType = assembly.GetType("InitOnlyStruct.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "InitOnlyStruct.Generated.PersonSerializer");

        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Elm");
        addressType.GetProperty("Unit")!.SetValue(address, 3);
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("HomeAddress")!.SetValue(node, address);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var rtAddress = nodeType.GetProperty("HomeAddress")!.GetValue(roundTripped)!;
        Assert.Equal("Elm", addressType.GetProperty("Street")!.GetValue(rtAddress));
        Assert.Equal(3, addressType.GetProperty("Unit")!.GetValue(rtAddress));
    }

    [Fact]
    public void CollectionOfStructComplexValues_RoundTrips()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace StructCollection;

            public struct Tag
            {
                public string Name { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public List<Tag> Tags { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("StructCollection.Person", throwOnError: true)!;
        var tagType = assembly.GetType("StructCollection.Tag", throwOnError: true)!;
        Assert.NotNull(assembly.GetType("StructCollection.Generated.TagSerializer", throwOnError: true));
        var serializer = CreateSerializer(assembly, "StructCollection.Generated.PersonSerializer");

        var tags = (System.Collections.IList)typeof(List<>).MakeGenericType(tagType)
            .GetConstructor(Type.EmptyTypes)!.Invoke(null)!;
        var tag = Activator.CreateInstance(tagType)!;
        tagType.GetProperty("Name")!.SetValue(tag, "vip");
        tags.Add(tag);
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("Tags")!.SetValue(node, tags);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var rtTags = (System.Collections.IList)nodeType.GetProperty("Tags")!.GetValue(roundTripped)!;
        Assert.Equal(typeof(List<>).MakeGenericType(tagType), rtTags.GetType());
        Assert.Single(rtTags);
        Assert.Equal("vip", tagType.GetProperty("Name")!.GetValue(rtTags[0]!));
    }

    [Fact]
    public void NullableStructComplexValues_RoundTripAndUseUnderlyingSerializer()
    {
        const string source = """
            using System.Collections.Generic;
            using Cvoya.Graph;

            namespace NullableStruct;

            public struct Address
            {
                public string Street { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address? HomeAddress { get; set; }
                public List<Address?> PreviousAddresses { get; set; } = new();
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("NullableStruct.Person", throwOnError: true)!;
        var addressType = assembly.GetType("NullableStruct.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "NullableStruct.Generated.PersonSerializer");
        Assert.NotNull(assembly.GetType("NullableStruct.Generated.AddressSerializer", throwOnError: true));

        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Pine");
        var nullableAddressType = typeof(Nullable<>).MakeGenericType(addressType);
        var addresses = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(nullableAddressType))!;
        addresses.Add(address);
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("HomeAddress")!.SetValue(node, address);
        nodeType.GetProperty("PreviousAddresses")!.SetValue(node, addresses);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var homeAddress = nodeType.GetProperty("HomeAddress")!.GetValue(roundTripped)!;
        Assert.Equal("Pine", addressType.GetProperty("Street")!.GetValue(homeAddress));
        var previousAddresses = (System.Collections.IList)nodeType.GetProperty("PreviousAddresses")!.GetValue(roundTripped)!;
        Assert.Single(previousAddresses);
        Assert.Equal("Pine", addressType.GetProperty("Street")!.GetValue(previousAddresses[0]!));
    }

    [Fact]
    public void ConstructorBoundPrivateSetterStruct_RoundTrips()
    {
        const string source = """
            using Cvoya.Graph;

            namespace ConstructorStruct;

            public struct Address
            {
                public Address(string street) => Street = street;
                public string Street { get; private set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("ConstructorStruct.Person", throwOnError: true)!;
        var addressType = assembly.GetType("ConstructorStruct.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "ConstructorStruct.Generated.PersonSerializer");
        var address = Activator.CreateInstance(addressType, ["Oak"])!;
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("HomeAddress")!.SetValue(node, address);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var homeAddress = nodeType.GetProperty("HomeAddress")!.GetValue(roundTripped)!;
        Assert.Equal("Oak", addressType.GetProperty("Street")!.GetValue(homeAddress));
    }

    [Fact]
    public void RequiredStructProperty_RoundTripsThroughObjectInitializer()
    {
        const string source = """
            using Cvoya.Graph;

            namespace RequiredStruct;

            public struct Address
            {
                public required string Street { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var nodeType = assembly.GetType("RequiredStruct.Person", throwOnError: true)!;
        var addressType = assembly.GetType("RequiredStruct.Address", throwOnError: true)!;
        var serializer = CreateSerializer(assembly, "RequiredStruct.Generated.PersonSerializer");
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Street")!.SetValue(address, "Cedar");
        var node = Activator.CreateInstance(nodeType)!;
        nodeType.GetProperty("HomeAddress")!.SetValue(node, address);

        var roundTripped = serializer.Deserialize(serializer.Serialize(node));

        var homeAddress = nodeType.GetProperty("HomeAddress")!.GetValue(roundTripped)!;
        Assert.Equal("Cedar", addressType.GetProperty("Street")!.GetValue(homeAddress));
    }

    [Fact]
    public void UnconstructibleReadonlyStruct_DoesNotEmitSerializers()
    {
        const string source = """
            using Cvoya.Graph;

            public readonly struct Address
            {
                public string Street { get; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);
        Assert.Contains("== No generated sources ==", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Serializer.g.cs", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongTypedStructConstructor_DoesNotEmitSerializers()
    {
        const string source = """
            using Cvoya.Graph;

            public readonly struct Address
            {
                public Address(int street) => Street = street.ToString();
                public string Street { get; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;

        var generated = GeneratorTestHelpers.RunGenerator(source);
        Assert.Contains("== No generated sources ==", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Serializer.g.cs", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void StructComplexProperty_ExposesNestedSchema()
    {
        const string source = """
            using Cvoya.Graph;

            namespace StructSchema;

            public struct Address
            {
                public string Street { get; set; }
            }

            [Node("Person")]
            public record Person : Node
            {
                public Address HomeAddress { get; set; }
            }
            """;
        var assembly = GeneratorTestHelpers.CompileAndLoadGeneratedAssembly(source);
        var personSerializer = assembly.GetType("StructSchema.Generated.PersonSerializer", throwOnError: true)!;

        var schema = (EntitySchema)personSerializer.GetMethod("GetSchemaStatic")!.Invoke(null, null)!;

        var homeAddress = Assert.Contains("HomeAddress", schema.ComplexProperties);
        Assert.NotNull(homeAddress.NestedSchema);
        Assert.Contains("Street", homeAddress.NestedSchema!.SimpleProperties.Keys);
    }

    private static IEntitySerializer CreateSerializer(System.Reflection.Assembly assembly, string serializerTypeName)
    {
        var serializerType = assembly.GetType(serializerTypeName, throwOnError: true)!;
        return Assert.IsAssignableFrom<IEntitySerializer>(Activator.CreateInstance(serializerType));
    }
}

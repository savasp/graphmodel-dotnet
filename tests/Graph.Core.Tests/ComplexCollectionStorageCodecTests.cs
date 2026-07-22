// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;

public class ComplexCollectionStorageCodecTests
{
    [Fact]
    public void Metadata_RehydratesExactNullLayoutAndDeclaredType()
    {
        var collection = new EntityCollection(
            typeof(AddressValue),
            [null, Entity("first"), null, null, Entity("last"), null]);
        var properties = PhysicalProperties("Addresses", collection);

        var result = ComplexCollectionStorageCodec.Rehydrate(
            "Addresses",
            typeof(AddressValue),
            properties,
            [(1, Entity("first")), (4, Entity("last"))],
            "Addresses");

        Assert.NotNull(result);
        Assert.Equal(typeof(AddressValue), result.Type);
        Assert.Collection(
            result.Entities,
            Assert.Null,
            Assert.NotNull,
            Assert.Null,
            Assert.Null,
            Assert.NotNull,
            Assert.Null);
    }

    [Fact]
    public void Metadata_DetectsMissingPersistedChild()
    {
        var collection = new EntityCollection(
            typeof(AddressValue),
            [Entity("first"), null, Entity("last")]);
        var properties = PhysicalProperties("Addresses", collection);

        var exception = Assert.Throws<GraphException>(() => ComplexCollectionStorageCodec.Rehydrate(
            "Addresses",
            typeof(AddressValue),
            properties,
            [(0, Entity("first"))]));

        Assert.Contains("index 2 has neither a child nor a null slot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_RequiresRelationshipTypeAndValidatesDeclaredMapping()
    {
        var properties = PhysicalProperties(
            "Addresses",
            new EntityCollection(typeof(AddressValue), [Entity("first")]));

        var mismatch = Assert.Throws<GraphException>(() => ComplexCollectionStorageCodec.Rehydrate(
            "Addresses",
            typeof(AddressValue),
            properties,
            [(0, Entity("first"))],
            "LIVES_AT"));
        Assert.Contains("stored relationship type 'Addresses'", mismatch.Message, StringComparison.Ordinal);

        properties.Remove(ComplexCollectionStorageCodec.GetRelationshipTypePropertyName("Addresses"));
        var partial = Assert.Throws<GraphException>(() => ComplexCollectionStorageCodec.Rehydrate(
            "Addresses",
            typeof(AddressValue),
            properties,
            [(0, Entity("first"))]));
        Assert.Contains("all four collection companions", partial.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataNamespace_DoesNotCollideWithUserProperty()
    {
        var logicalUserName = ComplexCollectionStorageCodec.GetLengthPropertyName("Addresses");
        var simpleProperties = new Dictionary<string, Property>
        {
            [logicalUserName] = new(
                typeof(Holder).GetProperty(nameof(Holder.Value))!,
                logicalUserName,
                Value: new SimpleValue("user", typeof(string))),
        };
        var encoded = SimpleCollectionStorageCodec.EncodeProperties(
            simpleProperties,
            omitNullPayloads: true,
            static value => value);
        foreach (var (name, value) in ComplexCollectionStorageCodec.EncodeProperties(
            ComplexProperties(new EntityCollection(typeof(AddressValue), [null])),
            static value => value))
        {
            encoded.Add(name, value);
        }

        var physical = encoded.ToDictionary(
            item => item.Key,
            item => ToGraphValue(item.Value),
            StringComparer.Ordinal);
        var decoded = SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true);
        var metadata = SimpleCollectionStorageCodec.ExtractComplexCollectionMetadata(physical);

        Assert.Equal("user", decoded[logicalUserName].ScalarValue);
        var collection = ComplexCollectionStorageCodec.Rehydrate(
            "Addresses",
            typeof(AddressValue),
            metadata,
            []);
        Assert.NotNull(collection);
        Assert.Single(collection.Entities);
        Assert.Null(collection.Entities[0]);
    }

    private static Dictionary<string, GraphValue> PhysicalProperties(
        string name,
        EntityCollection collection) =>
        ComplexCollectionStorageCodec.EncodeProperties(
                ComplexProperties(collection),
                static value => value)
            .ToDictionary(item => item.Key, item => ToGraphValue(item.Value), StringComparer.Ordinal);

    private static Dictionary<string, Property> ComplexProperties(EntityCollection collection) => new()
    {
        ["Addresses"] = new(
            typeof(Holder).GetProperty(nameof(Holder.Addresses))!,
            "Addresses",
            Value: collection),
    };

    private static GraphValue ToGraphValue(object? value) => value switch
    {
        IEnumerable values when value is not string and not byte[] =>
            GraphValue.List(values.Cast<object?>().Select(ToGraphValue).ToArray()),
        _ => GraphValue.Scalar(value),
    };

    private static EntityInfo Entity(string street) => new(
        typeof(AddressValue),
        nameof(AddressValue),
        [],
        new Dictionary<string, Property>
        {
            [nameof(AddressValue.Street)] = new(
                typeof(AddressValue).GetProperty(nameof(AddressValue.Street))!,
                nameof(AddressValue.Street),
                Value: new SimpleValue(street, typeof(string))),
        },
        new Dictionary<string, Property>());

    private sealed class Holder
    {
        public string Value { get; set; } = string.Empty;

        public List<AddressValue?> Addresses { get; set; } = [];
    }

    private sealed class AddressValue
    {
        public string Street { get; set; } = string.Empty;
    }
}

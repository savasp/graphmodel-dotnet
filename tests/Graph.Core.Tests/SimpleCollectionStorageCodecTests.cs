// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;

namespace Cvoya.Graph.Core.Tests;

public sealed class SimpleCollectionStorageCodecTests
{
    [Fact]
    public void Neo4jEncoding_ReconstructsNullPositionsAndElementType()
    {
        var encoded = SimpleCollectionStorageCodec.EncodeValue(
            "values",
            typeof(int?[]),
            new int?[] { null, 1, null, null, 2, null },
            omitNullPayloads: true,
            static value => value);
        var physical = encoded.ToDictionary(
            item => item.StorageName,
            item => Adapt(item.Value),
            StringComparer.Ordinal);

        var decoded = SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true)["values"];

        Assert.Equal(typeof(int?), decoded.CollectionElementType);
        Assert.Equal(
            new object?[] { null, 1, null, null, 2, null },
            decoded.Items.Select(item => item.ScalarValue));
        Assert.DoesNotContain("Version=", Assert.IsType<string>(
            physical[SimpleCollectionStorageCodec.GetElementTypePropertyName("values")].ScalarValue));
    }

    [Fact]
    public void NativeNullList_ValidatesCompanionAndPreservesAllNullAndEmptyTypes()
    {
        foreach (var values in new[]
        {
            new int?[] { null, null },
            Array.Empty<int?>(),
        })
        {
            var physical = SimpleCollectionStorageCodec.EncodeValue(
                    "values",
                    typeof(int?[]),
                    values,
                    omitNullPayloads: false,
                    static value => value)
                .ToDictionary(item => item.StorageName, item => Adapt(item.Value), StringComparer.Ordinal);

            var decoded = SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: false)["values"];

            Assert.Equal(typeof(int?), decoded.CollectionElementType);
            Assert.Equal(values, decoded.Items.Select(item => item.ScalarValue).Cast<int?>());
        }
    }

    [Fact]
    public void ReservedLogicalName_IsEscapedAndRoundTripsWithoutMetadataExposure()
    {
        const string logicalName = "__cvoya_sc:v1:t:dmFsdWVz";
        var encoded = SimpleCollectionStorageCodec.EncodeValue(
                "values",
                typeof(string?[]),
                new string?[] { null, "ordinary" },
                omitNullPayloads: true,
                static value => value)
            .Concat(SimpleCollectionStorageCodec.EncodeValue(
                logicalName,
                typeof(string?[]),
                new string?[] { "value", null },
                omitNullPayloads: true,
                static value => value));
        var physical = encoded.ToDictionary(
            item => item.StorageName,
            item => Adapt(item.Value),
            StringComparer.Ordinal);

        var decoded = SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true);

        Assert.Equal(2, decoded.Count);
        Assert.True(decoded.ContainsKey(logicalName));
        Assert.DoesNotContain(decoded.Keys, name => name.StartsWith(SimpleCollectionStorageCodec.Prefix, StringComparison.Ordinal) && name != logicalName);
    }

    [Fact]
    public void MalformedOrOrphanedCompanions_FailTheWholePropertyMap()
    {
        var payloadName = SimpleCollectionStorageCodec.GetPayloadPropertyName("values");
        var nullIndexesName = SimpleCollectionStorageCodec.GetNullIndexesPropertyName("values");
        var typeName = SimpleCollectionStorageCodec.GetElementTypePropertyName("values");
        var malformed = new Dictionary<string, GraphValue>(StringComparer.Ordinal)
        {
            [payloadName] = GraphValue.List([GraphValue.Scalar(1)]),
            [nullIndexesName] = GraphValue.List([GraphValue.Scalar(2)]),
            [typeName] = GraphValue.Scalar(SimpleCollectionStorageCodec.GetTypeIdentity(typeof(int?))),
        };
        var orphaned = new Dictionary<string, GraphValue>(StringComparer.Ordinal)
        {
            [typeName] = GraphValue.Scalar(SimpleCollectionStorageCodec.GetTypeIdentity(typeof(int?))),
        };

        Assert.Throws<GraphException>(() =>
            SimpleCollectionStorageCodec.DecodeProperties(malformed, payloadOmitsNulls: true));
        Assert.Throws<GraphException>(() =>
            SimpleCollectionStorageCodec.DecodeProperties(orphaned, payloadOmitsNulls: true));
    }

    [Theory]
    [InlineData("1")]
    [InlineData(1.0)]
    public void NonIntegerNullIndex_FailsTheWholePropertyMap(object invalidIndex)
    {
        var physical = SimpleCollectionStorageCodec.EncodeValue(
                "values",
                typeof(int?[]),
                new int?[] { null },
                omitNullPayloads: true,
                static value => value)
            .ToDictionary(item => item.StorageName, item => Adapt(item.Value), StringComparer.Ordinal);
        physical[SimpleCollectionStorageCodec.GetNullIndexesPropertyName("values")] =
            GraphValue.List([GraphValue.Scalar(invalidIndex)]);

        Assert.Throws<GraphException>(() =>
            SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true));
    }

    [Fact]
    public void NonSimpleElementType_FailsTheWholePropertyMap()
    {
        var physical = SimpleCollectionStorageCodec.EncodeValue(
                "values",
                typeof(int[]),
                new List<int> { 1 },
                omitNullPayloads: true,
                static value => value)
            .ToDictionary(item => item.StorageName, item => Adapt(item.Value), StringComparer.Ordinal);
        physical[SimpleCollectionStorageCodec.GetElementTypePropertyName("values")] =
            GraphValue.Scalar(SimpleCollectionStorageCodec.GetTypeIdentity(typeof(List<int>)));

        Assert.Throws<GraphException>(() =>
            SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true));
    }

    [Fact]
    public void NonCanonicalElementType_FailsTheWholePropertyMap()
    {
        var physical = SimpleCollectionStorageCodec.EncodeValue(
                "values",
                typeof(int[]),
                new List<int> { 1 },
                omitNullPayloads: true,
                static value => value)
            .ToDictionary(item => item.StorageName, item => Adapt(item.Value), StringComparer.Ordinal);
        physical[SimpleCollectionStorageCodec.GetElementTypePropertyName("values")] =
            GraphValue.Scalar(typeof(int).FullName);

        Assert.Throws<GraphException>(() =>
            SimpleCollectionStorageCodec.DecodeProperties(physical, payloadOmitsNulls: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ByteArrayValue_EncodesAsScalarPayload(bool declared)
    {
        var value = new byte[] { 1, 2, 3 };

        var encoded = SimpleCollectionStorageCodec.EncodeValue(
            "data",
            declared ? typeof(byte[]) : null,
            value,
            omitNullPayloads: true,
            static item => item);

        Assert.Equal(3, encoded.Count);
        Assert.Same(value, encoded.Single(item => item.StorageName == "data").Value);
        Assert.Null(encoded.Single(item =>
            item.StorageName == SimpleCollectionStorageCodec.GetNullIndexesPropertyName("data")).Value);
        Assert.Null(encoded.Single(item =>
            item.StorageName == SimpleCollectionStorageCodec.GetElementTypePropertyName("data")).Value);
    }

    [Fact]
    public void ScalarValue_AlwaysClearsCollectionCompanions()
    {
        var encoded = SimpleCollectionStorageCodec.EncodeValue(
            "name",
            typeof(string),
            "value",
            omitNullPayloads: true,
            static item => item);

        Assert.Equal(
            new (string StorageName, object? Value)[]
            {
                ("name", "value"),
                (SimpleCollectionStorageCodec.GetNullIndexesPropertyName("name"), null),
                (SimpleCollectionStorageCodec.GetElementTypePropertyName("name"), null),
            },
            encoded.Select(item => (item.StorageName, item.Value)));
    }

    private static GraphValue Adapt(object? value) => value switch
    {
        System.Collections.IEnumerable sequence when value is not string and not byte[] =>
            GraphValue.List(sequence.Cast<object?>().Select(Adapt).ToArray()),
        _ => GraphValue.Scalar(value),
    };
}

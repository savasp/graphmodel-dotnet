// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.Serialization.Results;
using Npgsql.Age.Types;

public sealed class AgeRecordAdapterTests
{
    [Fact]
    public void AdaptsAnnotatedVertexInsideMap()
    {
        var value = new Agtype(
            """{"Node":{"id":844424930131969,"label":"Manager","properties":{"Id":"domain-1","inheritance_labels":["Manager","Person"]}}::vertex,"ComplexProperties":[[]]}""");
        var record = new AgeRecord(new Dictionary<string, object?> { ["Node"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["Node"];

        Assert.Equal(GraphValueKind.Map, adapted.Kind);
        Assert.True(
            adapted.Entries["Node"].Kind == GraphValueKind.Node,
            $"Nested value was {adapted.Entries["Node"].Kind} / {adapted.Entries["Node"].ScalarValue?.GetType().FullName}: {adapted.Entries["Node"].ScalarValue}");
        Assert.Equal(["Manager", "Person"], adapted.Entries["Node"].Labels);
        Assert.Empty(adapted.Entries["ComplexProperties"].Items);
    }

    [Fact]
    public void NativeVertexUsesPhysicalLogicalLabelWithoutMetadata()
    {
        var value = new Agtype(
            """{"id":844424930131969,"label":"Person","properties":{"FirstName":"Raw"}}::vertex""");
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal(["Person"], adapted.Labels);
        Assert.DoesNotContain("CvoyaNode", adapted.Labels);
    }

    [Theory]
    [InlineData("CvoyaN_NOT_AN_ENCODED_LABEL")]
    [InlineData("CvoyaN_0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void NativeVertexWithEncodedStyleLabelRemainsVisibleWithoutMetadata(string label)
    {
        var value = new Agtype(
            $"{{\"id\":844424930131969,\"label\":\"{label}\",\"properties\":{{\"FirstName\":\"Raw\"}}}}::vertex");
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal([label], adapted.Labels);
    }

    [Fact]
    public void UnmarkedRetiredStorageVertexRemainsNative()
    {
        var value = new Agtype(
            """{"id":844424930131969,"label":"CvoyaNode","properties":{"inheritance_labels":["Person"]}}::vertex""");
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal(["CvoyaNode"], adapted.Labels);
    }

    [Theory]
    [InlineData("KNOWS", "IGNORED", "KNOWS")]
    [InlineData("CvoyaRelationship", "KNOWS", "CvoyaRelationship")]
    public void UnmarkedRelationshipUsesNativeType(
        string physicalType,
        string storedType,
        string expectedType)
    {
        var value = new Agtype(
            $"{{\"id\":1125899906842625,\"label\":\"{physicalType}\",\"start_id\":844424930131969," +
            $"\"end_id\":844424930131970,\"properties\":{{\"Type\":\"{storedType}\"}}}}::edge");
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal(expectedType, adapted.RelationshipType);
    }

    [Fact]
    public void MarkedComplexRelationshipUsesStoredHierarchyType()
    {
        var value = new Agtype(
            """{"id":1125899906842625,"label":"CvoyaRelationship","start_id":844424930131969,"end_id":844424930131970,"properties":{"__graphModelComplexProperty":true,"inheritance_labels":["Address"]}}::edge""");
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = value });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal("Address", adapted.RelationshipType);
        Assert.DoesNotContain("__graphModelComplexProperty", adapted.Entries);
    }

    [Theory]
    [InlineData("9223372036854775807", typeof(long))]
    [InlineData("1234567890.123456789::numeric", typeof(decimal))]
    [InlineData("true", typeof(bool))]
    public void PreservesScalarKinds(string text, Type expectedType)
    {
        var record = new AgeRecord(new Dictionary<string, object?> { ["value"] = new Agtype(text) });

        var adapted = new AgeRecordAdapter().Adapt(record)["value"];

        Assert.Equal(GraphValueKind.Scalar, adapted.Kind);
        Assert.IsType(expectedType, adapted.ScalarValue);
    }
}

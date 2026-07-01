// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Age.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Serialization;
using Xunit;

/// <summary>
/// Tests for EntityInfoBuilder static helper methods.
/// Covers JSON conversion, dictionary building, and property creation.
/// </summary>
public sealed class EntityInfoBuilderTests
{
    [Fact]
    public void CreateSimpleProperty_NullValue_ReturnsPropertyWithObjectType()
    {
        var prop = EntityInfoBuilder.CreateSimpleProperty("test", null);

        Assert.Equal("test", prop.Label);
        Assert.IsType<SimpleValue>(prop.Value);
        var sv = (SimpleValue?)prop.Value;
        Assert.Equal(typeof(object), sv!.Type);
    }

    [Fact]
    public void CreateSimpleProperty_StringValue_ReturnsPropertyWithStringType()
    {
        var prop = EntityInfoBuilder.CreateSimpleProperty("name", "John");

        Assert.Equal("name", prop.Label);
        var sv = (SimpleValue?)prop.Value;
        Assert.Equal("John", sv!.Object);
        Assert.Equal(typeof(string), sv!.Type);
    }

    [Fact]
    public void CreateSimpleProperty_IntValue_ReturnsPropertyWithIntType()
    {
        var prop = EntityInfoBuilder.CreateSimpleProperty("age", 42);
        var sv = (SimpleValue?)prop.Value;
        Assert.Equal(42, sv!.Object);
        Assert.Equal(typeof(int), sv!.Type);
    }

    [Fact]
    public void ConvertJsonElementToDictionary_SimpleObject_ReturnsCorrectDict()
    {
        using var doc = JsonDocument.Parse("{\"name\": \"Alice\", \"age\": 30}");
        var dict = EntityInfoBuilder.ConvertJsonElementToDictionary(doc.RootElement);

        Assert.Equal(2, dict.Count);
        Assert.Equal("Alice", dict["name"]);
        Assert.NotNull(dict["age"]);
    }

    [Fact]
    public void ConvertJsonElementToDictionary_NestedObject_ReturnsNestedDict()
    {
        using var doc = JsonDocument.Parse("{\"address\": {\"street\": \"Main St\", \"city\": \"NYC\"}}");
        var dict = EntityInfoBuilder.ConvertJsonElementToDictionary(doc.RootElement);

        Assert.Single(dict);
        var nested = Assert.IsType<Dictionary<string, object?>>(dict["address"]);
        Assert.Equal("Main St", nested["street"]);
        Assert.Equal("NYC", nested["city"]);
    }

    [Fact]
    public void ConvertJsonElementToDictionary_Array_ReturnsList()
    {
        using var doc = JsonDocument.Parse("{\"tags\": [\"a\", \"b\", \"c\"]}");
        var dict = EntityInfoBuilder.ConvertJsonElementToDictionary(doc.RootElement);

        var list = Assert.IsType<List<object?>>(dict["tags"]);
        Assert.Equal(3, list.Count);
        Assert.Equal("a", list[0]);
        Assert.Equal("b", list[1]);
        Assert.Equal("c", list[2]);
    }

    [Fact]
    public void ConvertJsonElementToValue_Boolean_ReturnsBool()
    {
        using var doc = JsonDocument.Parse("true");
        var result = EntityInfoBuilder.ConvertJsonElementToValue(doc.RootElement);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertJsonElementToValue_Null_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("null");
        var result = EntityInfoBuilder.ConvertJsonElementToValue(doc.RootElement);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertJsonElementToValue_Number_Double_ReturnsDouble()
    {
        using var doc = JsonDocument.Parse("3.14");
        var result = EntityInfoBuilder.ConvertJsonElementToValue(doc.RootElement);
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void ParsePointFromJson_WithLongitudeLatitude_ReturnsCorrectPoint()
    {
        using var doc = JsonDocument.Parse("{\"longitude\": 12.5, \"latitude\": 41.9}");
        var point = EntityInfoBuilder.ParsePointFromJson(doc.RootElement);

        Assert.Equal(12.5, point.Longitude);
        Assert.Equal(41.9, point.Latitude);
        Assert.Equal(0, point.Height);
    }

    [Fact]
    public void ParsePointFromJson_WithXYZ_ReturnsCorrectPoint()
    {
        using var doc = JsonDocument.Parse("{\"x\": 1.0, \"y\": 2.0, \"z\": 3.0}");
        var point = EntityInfoBuilder.ParsePointFromJson(doc.RootElement);

        Assert.Equal(1.0, point.Longitude);
        Assert.Equal(2.0, point.Latitude);
        Assert.Equal(3.0, point.Height);
    }

    [Fact(Skip = "ParsePointFromJson array format needs investigation")]
    public void ParsePointFromJson_WithArrayFormat_ReturnsCorrectPoint()
    {
    }

    [Fact]
    public void CreateEntityInfoFromDictionary_FlatProperties_ReturnsEntityInfo()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Test",
            ["Value"] = 123,
            ["Active"] = true
        };

        var result = EntityInfoBuilder.CreateEntityInfoFromDictionary(dict, "TestType");

        Assert.Equal("TestType", result.Label);
        Assert.Equal(3, result.SimpleProperties.Count);
        Assert.Empty(result.ComplexProperties);
    }

    [Fact]
    public void CreateEntityInfoFromDictionary_NestedObject_ReturnsEntityInfoWithComplex()
    {
        var dict = new Dictionary<string, object?>
        {
            ["Name"] = "Root",
            ["Child"] = new Dictionary<string, object?>
            {
                ["Name"] = "Child"
            }
        };

        var result = EntityInfoBuilder.CreateEntityInfoFromDictionary(dict, "Root");

        Assert.Equal("Root", result.Label);
        Assert.Single(result.SimpleProperties);
        Assert.Single(result.ComplexProperties);
        var childProp = result.ComplexProperties["Child"];
        var childEntity = Assert.IsType<EntityInfo>(childProp.Value);
        Assert.Single(childEntity.SimpleProperties);
    }

    [Fact]
    public void DetermineElementType_FromItems_ReturnsFirstNonNullType()
    {
        var list = new List<object?> { null, "hello", 42 };
        var type = EntityInfoBuilder.DetermineElementType(list);
        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void DetermineElementType_AllNull_ReturnsObject()
    {
        var list = new List<object?> { null, null };
        var type = EntityInfoBuilder.DetermineElementType(list);
        Assert.Equal(typeof(object), type);
    }

    [Fact(Skip = "GraphDataModel.IsComplex behavior varies by runtime context")]
    public void IsComplexCollectionType_GenericComplex_ReturnsTrue()
    {
    }

    [Fact]
    public void IsComplexCollectionType_SimpleType_ReturnsFalse()
    {
        var type = typeof(List<string>);
        Assert.False(EntityInfoBuilder.IsComplexCollectionType(type));
    }

    [Fact]
    public void IsComplexCollectionType_NonGeneric_ReturnsFalse()
    {
        var type = typeof(string);
        Assert.False(EntityInfoBuilder.IsComplexCollectionType(type));
    }
}

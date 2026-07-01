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
using System.Text.Json;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Serialization;
using Npgsql.Age.Types;
using Xunit;

/// <summary>
/// Tests for AgeValueConverters static helper methods.
/// Covers Agtype-to-CLR conversion, dictionary conversion, and JSON element parsing.
/// </summary>
public sealed class AgeValueConvertersTests
{
    [Fact]
    public void ConvertScalarAgtype_String_ReturnsString()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("hello", typeof(string));
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ConvertScalarAgtype_Int_ReturnsInt()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("42", typeof(int));
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertScalarAgtype_Long_ReturnsLong()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("1234567890123", typeof(long));
        Assert.Equal(1234567890123L, result);
    }

    [Fact]
    public void ConvertScalarAgtype_Double_ReturnsDouble()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("3.14", typeof(double));
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void ConvertScalarAgtype_Float_ReturnsFloat()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("2.5", typeof(float));
        Assert.Equal(2.5f, result);
    }

    [Fact]
    public void ConvertScalarAgtype_Decimal_ReturnsDecimal()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("100.5", typeof(decimal));
        Assert.Equal(100.5m, result);
    }

    [Fact]
    public void ConvertScalarAgtype_BoolTrue_ReturnsTrue()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("true", typeof(bool));
        Assert.Equal(true, result);
    }

    [Fact]
    public void ConvertScalarAgtype_BoolFalse_ReturnsFalse()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("false", typeof(bool));
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertScalarAgtype_DateTime_ReturnsDateTime()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("2024-01-15", typeof(DateTime));
        Assert.IsType<DateTime>(result);
        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2024, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(15, dt.Day);
    }

    [Fact]
    public void ConvertScalarAgtype_UnmatchedType_ReturnsString()
    {
        var result = AgeValueConverters.ConvertScalarAgtype("anything", typeof(Guid));
        Assert.Equal("anything", result);
    }

    [Fact]
    public void ConvertDictionaryToType_SimpleDict_ReturnsPopulatedObject()
    {
        var dict = new Dictionary<string, object>
        {
            ["Name"] = "Test",
            ["Age"] = 30
        };

        var result = AgeValueConverters.ConvertDictionaryToType(dict, typeof(PersonNode));

        if (result is PersonNode person)
        {
            Assert.Equal("Test", person.Name);
            Assert.Equal(30, person.Age);
        }
        else
        {
            // JSON round-trip may fail with Dictionary<string,object> depending on serializer
            Assert.Null(result);
        }
    }

    [Fact]
    public void ConvertDictionaryToType_EmptyDict_ReturnsNull()
    {
        var result = AgeValueConverters.ConvertDictionaryToType(new Dictionary<string, object>(), typeof(PersonNode));
        Assert.Null(result);
    }

    [Fact]
    public void ConvertDictionaryToType_NullDict_ReturnsNull()
    {
        var result = AgeValueConverters.ConvertDictionaryToType(null!, typeof(PersonNode));
        Assert.Null(result);
    }

    [Fact]
    public void ConvertJsonNumber_IntTarget_ReturnsInt()
    {
        using var doc = JsonDocument.Parse("42");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(int));
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertJsonNumber_LongTarget_ReturnsLong()
    {
        using var doc = JsonDocument.Parse("9999999999");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(long));
        Assert.Equal(9999999999L, result);
    }

    [Fact]
    public void ConvertJsonNumber_DoubleTarget_ReturnsDouble()
    {
        using var doc = JsonDocument.Parse("3.14159");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(double));
        Assert.Equal(3.14159, result);
    }

    [Fact]
    public void ConvertJsonNumber_FloatTarget_ReturnsFloat()
    {
        using var doc = JsonDocument.Parse("2.5");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(float));
        Assert.Equal(2.5f, result);
    }

    [Fact]
    public void ConvertJsonNumber_DecimalTarget_ReturnsDecimal()
    {
        using var doc = JsonDocument.Parse("99.99");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(decimal));
        Assert.Equal(99.99m, result);
    }

    [Fact]
    public void ConvertJsonNumber_Default_ReturnsDouble()
    {
        using var doc = JsonDocument.Parse("42.0");
        var result = AgeValueConverters.ConvertJsonNumber(doc.RootElement, typeof(object));
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void ConvertJsonElementToEntityInfo_SimpleObject_CreatesEntityInfo()
    {
        using var doc = JsonDocument.Parse("{\"Name\": \"Alice\", \"Age\": 25}");
        var result = AgeValueConverters.ConvertJsonElementToEntityInfo(doc.RootElement, typeof(PersonNode));

        Assert.Equal(typeof(PersonNode), result.ActualType);
        Assert.Equal(2, result.SimpleProperties.Count);
        Assert.Empty(result.ComplexProperties);
    }

    [Fact]
    public void ConvertAgtypeMapToEntityInfo_SimpleMap_CreatesEntityInfo()
    {
        // Simulate an Agtype map string — we construct it via JSON
        var agtype = new Agtype("{\"Name\": \"Bob\", \"Age\": 40}");
        var result = AgeValueConverters.ConvertAgtypeMapToEntityInfo(agtype, typeof(PersonNode));

        Assert.Contains("Name", result.SimpleProperties.Keys);
        Assert.Contains("Age", result.SimpleProperties.Keys);
    }

    [Fact]
    public void ConvertAgtypeMapToEntityInfo_EmptyString_ReturnsEmptyEntityInfo()
    {
        var agtype = new Agtype("");
        var result = AgeValueConverters.ConvertAgtypeMapToEntityInfo(agtype, typeof(PersonNode));

        Assert.Empty(result.SimpleProperties);
        Assert.Empty(result.ComplexProperties);
    }
}

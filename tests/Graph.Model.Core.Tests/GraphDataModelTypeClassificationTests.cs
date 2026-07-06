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

namespace Cvoya.Graph.Model.Core.Tests;

using System.Collections;
using System.Collections.ObjectModel;


[Trait("Area", "GraphDataModel")]
public class GraphDataModelTypeClassificationTests
{
    public static TheoryData<Type, bool> SimpleTypeCases => new()
    {
        { typeof(bool), true },
        { typeof(byte), true },
        { typeof(sbyte), true },
        { typeof(short), true },
        { typeof(ushort), true },
        { typeof(int), true },
        { typeof(uint), true },
        { typeof(long), true },
        { typeof(ulong), true },
        { typeof(char), true },
        { typeof(float), true },
        { typeof(double), true },
        { typeof(decimal), true },
        { typeof(GraphDataModelTestEnum), true },
        { typeof(string), true },
        { typeof(Point), true },
        { typeof(DateTime), true },
        { typeof(DateTimeOffset), true },
        { typeof(TimeSpan), true },
        { typeof(TimeOnly), true },
        { typeof(DateOnly), true },
        { typeof(Guid), true },
        { typeof(byte[]), true },
        { typeof(Uri), true },
        { typeof(bool?), true },
        { typeof(int?), true },
        { typeof(decimal?), true },
        { typeof(GraphDataModelTestEnum?), true },
        { typeof(Point?), true },
        { typeof(DateTime?), true },
        { typeof(DateTimeOffset?), true },
        { typeof(TimeSpan?), true },
        { typeof(TimeOnly?), true },
        { typeof(DateOnly?), true },
        { typeof(Guid?), true },
        { typeof(object), false },
        { typeof(FlatValueObject), false },
        { typeof(RecursiveValueObject), false },
        { typeof(SimpleStruct), false },
        { typeof(int[]), false },
        { typeof(string[]), false },
        { typeof(List<int>), false },
        { typeof(List<FlatValueObject>), false },
        { typeof(Dictionary<string, int>), false },
    };

    public static TheoryData<Type, bool> CollectionOfSimpleCases => new()
    {
        { typeof(string), false },
        { typeof(byte[]), true },
        { typeof(int[]), true },
        { typeof(int?[]), true },
        { typeof(string[]), true },
        { typeof(Point[]), true },
        { typeof(Uri[]), true },
        { typeof(GraphDataModelTestEnum[]), true },
        { typeof(List<int>), true },
        { typeof(List<int?>), true },
        { typeof(List<string>), true },
        { typeof(IReadOnlyList<Guid>), true },
        { typeof(IEnumerable<DateOnly>), true },
        { typeof(HashSet<TimeOnly>), true },
        { typeof(List<byte[]>), true },
        { typeof(ArrayList), false },
        { typeof(List<object>), false },
        { typeof(List<List<int>>), false },
        { typeof(List<FlatValueObject>), false },
        { typeof(Dictionary<string, int>), false },
        { typeof(IDictionary<string, int>), false },
        { typeof(IReadOnlyDictionary<string, int>), false },
        { typeof(Dictionary<string, FlatValueObject>), false },
    };

    public static TheoryData<Type, bool> CollectionOfComplexCases => new()
    {
        { typeof(string), false },
        { typeof(List<int>), false },
        { typeof(List<string>), false },
        { typeof(int[]), false },
        { typeof(List<object>), false },
        { typeof(object[]), false },
        { typeof(FlatValueObject[]), true },
        { typeof(List<FlatValueObject>), true },
        { typeof(IReadOnlyList<FlatValueObject>), true },
        { typeof(List<RecursiveValueObject>), true },
        { typeof(List<SimpleStruct>), true },
        { typeof(List<List<FlatValueObject>>), false },
        { typeof(Dictionary<string, FlatValueObject>), false },
        { typeof(IDictionary<string, FlatValueObject>), false },
        { typeof(IReadOnlyDictionary<string, FlatValueObject>), false },
    };

    public static TheoryData<Type, bool> ComplexCases => new()
    {
        { typeof(string), false },
        { typeof(int), false },
        { typeof(int?), false },
        { typeof(Point), false },
        { typeof(Uri), false },
        { typeof(byte[]), false },
        { typeof(List<int>), false },
        { typeof(List<FlatValueObject>), false },
        { typeof(Dictionary<string, int>), false },
        { typeof(IDictionary<string, int>), false },
        { typeof(IReadOnlyDictionary<string, int>), false },
        { typeof(object), false },
        { typeof(GraphDataModelTestEnum), false },
        { typeof(FlatValueObject), true },
        { typeof(RecursiveValueObject), true },
        { typeof(SimpleStruct), true },
    };

    public static TheoryData<Type, bool> DictionaryCases => new()
    {
        { typeof(Dictionary<string, int>), true },
        { typeof(IDictionary<string, int>), true },
        { typeof(IReadOnlyDictionary<string, int>), true },
        { typeof(Dictionary<string, FlatValueObject>), true },
        { typeof(ReadOnlyDictionary<string, int>), true },
        { typeof(List<int>), false },
        { typeof(int[]), false },
        { typeof(string), false },
        { typeof(FlatValueObject), false },
    };

    public static TheoryData<string> PropertyNameCases => new()
    {
        "Address",
        "phone_numbers",
        "UPPER",
        "already__contains__suffix",
        "__PROPERTY__already__",
        "",
        " ",
        "Name With Spaces",
    };

    public static TheoryData<string, string> RelationshipTypeParseCases => new()
    {
        { "__PROPERTY__Address__", "Address" },
        { "__PROPERTY____", "" },
        { "__PROPERTY__Name With Spaces__", "Name With Spaces" },
        { "KNOWS", "KNOWS" },
        { "PROPERTY__Address__", "PROPERTY__Address__" },
        { "__PROPERTY__Address", "__PROPERTY__Address" },
        { "Address__", "Address__" },
        { "", "" },
    };

    [Theory]
    [MemberData(nameof(SimpleTypeCases))]
    public void IsSimple_ReturnsExpectedResult(Type type, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsSimple(type));
    }

    [Theory]
    [MemberData(nameof(CollectionOfSimpleCases))]
    public void IsCollectionOfSimple_ReturnsExpectedResult(Type type, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsCollectionOfSimple(type));
    }

    [Theory]
    [MemberData(nameof(CollectionOfComplexCases))]
    public void IsCollectionOfComplex_ReturnsExpectedResult(Type type, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsCollectionOfComplex(type));
    }

    [Theory]
    [MemberData(nameof(ComplexCases))]
    public void IsComplex_ReturnsExpectedResult(Type type, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsComplex(type));
    }

    [Theory]
    [MemberData(nameof(DictionaryCases))]
    public void IsDictionary_ReturnsExpectedResult(Type type, bool expected)
    {
        Assert.Equal(expected, GraphDataModel.IsDictionary(type));
    }

    [Theory]
    [MemberData(nameof(PropertyNameCases))]
    public void PropertyNameToRelationshipTypeName_UsesExactSentinelFormatAndRoundTrips(string propertyName)
    {
        var relationshipTypeName = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);

        Assert.Equal(
            $"{GraphDataModel.PropertyRelationshipTypeNamePrefix}{propertyName}{GraphDataModel.PropertyRelationshipTypeNameSuffix}",
            relationshipTypeName);
        Assert.Equal(propertyName, GraphDataModel.RelationshipTypeNameToPropertyName(relationshipTypeName));
    }

    [Theory]
    [MemberData(nameof(RelationshipTypeParseCases))]
    public void RelationshipTypeNameToPropertyName_ParsesOnlySentinelWrappedNames(string relationshipTypeName, string expected)
    {
        Assert.Equal(expected, GraphDataModel.RelationshipTypeNameToPropertyName(relationshipTypeName));
    }

    private enum GraphDataModelTestEnum
    {
        One,
    }

    private readonly record struct SimpleStruct(int Value);

    private sealed class FlatValueObject
    {
        public string Street { get; init; } = string.Empty;

        public int Unit { get; init; }
    }

    private sealed class RecursiveValueObject
    {
        public RecursiveValueObject? Next { get; set; }
    }
}

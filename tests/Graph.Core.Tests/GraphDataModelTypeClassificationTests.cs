// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;


/// <summary>
/// Runtime truth table for <see cref="GraphDataModel"/> type classification. Mirrored by
/// <c>Cvoya.Graph.Analyzers.Tests.AnalyzerHelperTypeClassificationTests</c> and
/// <c>Cvoya.Graph.Serialization.CodeGen.Tests.GraphDataModelTypeClassificationTests</c>, which
/// contain a matching named-simple-type slice for the analyzer's and source generator's independent
/// implementations; keep Point, the temporal types, Guid, and Uri aligned across all three (#387).
/// </summary>
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
        { typeof(IntPtr), false },
        { typeof(UIntPtr), false },
        { typeof(GraphDataModelTestEnum), true },
        { typeof(string), true },
        { typeof(Point), true },
        { typeof(System.Drawing.Point), false },
        { typeof(System.Drawing.Point?), false },
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
        { typeof(IntPtr?), false },
        { typeof(UIntPtr?), false },
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
        { typeof(IntPtr[]), false },
        { typeof(UIntPtr?[]), false },
        { typeof(string[]), true },
        { typeof(Point[]), true },
        { typeof(Uri[]), true },
        { typeof(GraphDataModelTestEnum[]), true },
        { typeof(List<int>), true },
        { typeof(List<int?>), true },
        { typeof(List<IntPtr>), false },
        { typeof(List<UIntPtr?>), false },
        { typeof(List<string>), true },
        { typeof(List<Point>), true },
        { typeof(List<System.Drawing.Point>), false },
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
        { typeof(List<System.Drawing.Point>), true },
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
        { typeof(System.Drawing.Point), true },
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
        "already_contains_suffix",
        "Name With Spaces",
    };

    public static TheoryData<string> RelationshipTypeParseCases => new()
    {
        "Address",
        "KNOWS",
        "Name With Spaces",
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
    public void PropertyNameToRelationshipTypeName_UsesSemanticNameAndRoundTrips(string propertyName)
    {
        var relationshipTypeName = GraphDataModel.PropertyNameToRelationshipTypeName(propertyName);

        Assert.Equal(propertyName, relationshipTypeName);
        Assert.Equal(propertyName, GraphDataModel.RelationshipTypeNameToPropertyName(relationshipTypeName));
    }

    [Theory]
    [MemberData(nameof(RelationshipTypeParseCases))]
    public void RelationshipTypeNameToPropertyName_PreservesSemanticName(string relationshipTypeName)
    {
        Assert.Equal(relationshipTypeName, GraphDataModel.RelationshipTypeNameToPropertyName(relationshipTypeName));
    }

    [Fact]
    public void GetComplexPropertyRelationshipType_UsesAttributeOverride()
    {
        var property = typeof(AttributedNode).GetProperty(nameof(AttributedNode.Home))!;

        Assert.Equal("LIVES_AT", GraphDataModel.GetComplexPropertyRelationshipType(property));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void PropertyNameToRelationshipTypeName_RejectsNullOrWhitespace(string? propertyName)
    {
        Assert.ThrowsAny<ArgumentException>(() => GraphDataModel.PropertyNameToRelationshipTypeName(propertyName!));
    }

    [Fact]
    public void EnsureComplexPropertyDepth_WalksThroughComplexStructs()
    {
        var allowed = new StructDepthNode
        {
            Holder = new StructHolder(CreateChain(GraphDataModel.DefaultDepthAllowed - 1)),
        };
        var tooDeep = new StructDepthNode
        {
            Holder = new StructHolder(CreateChain(GraphDataModel.DefaultDepthAllowed)),
        };

        allowed.EnsureComplexPropertyDepth();
        var exception = Assert.Throws<GraphException>(() => tooDeep.EnsureComplexPropertyDepth());

        Assert.Contains(GraphDataModel.DefaultDepthAllowed.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureComplexPropertyDepth_AllowsDefaultDepthAndRejectsDeeperGraph()
    {
        var allowed = new DepthNode { Value = CreateChain(GraphDataModel.DefaultDepthAllowed) };
        var tooDeep = new DepthNode { Value = CreateChain(GraphDataModel.DefaultDepthAllowed + 1) };

        allowed.EnsureComplexPropertyDepth();
        var exception = Assert.Throws<GraphException>(() => tooDeep.EnsureComplexPropertyDepth());

        Assert.Contains(GraphDataModel.DefaultDepthAllowed.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    private static DepthValue CreateChain(int depth)
    {
        var root = new DepthValue();
        var current = root;
        for (var index = 1; index < depth; index++)
        {
            current.Next = new DepthValue();
            current = current.Next;
        }

        return root;
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

    private sealed record AttributedNode : Node
    {
        [ComplexProperty(RelationshipType = "LIVES_AT")]
        public FlatValueObject Home { get; init; } = new();
    }

    private sealed record DepthNode : Node
    {
        public DepthValue Value { get; init; } = new();
    }

    private readonly record struct StructHolder(DepthValue? Inner);

    private sealed record StructDepthNode : Node
    {
        public StructHolder Holder { get; init; }
    }

    private sealed class DepthValue
    {
        public DepthValue? Next { get; set; }
    }
}

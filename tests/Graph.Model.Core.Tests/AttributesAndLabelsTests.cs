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

using System.Reflection;


[Trait("Area", "Attributes")]
public class AttributesAndLabelsTests
{
    public static TheoryData<Type, string> TypeLabelCases => new()
    {
        { typeof(PlainNode), nameof(PlainNode) },
        { typeof(AttributedNode), "CoreAttributedNode" },
        { typeof(AttributedRelationship), "CORE_ATTRIBUTED_REL" },
        { typeof(GenericNode<>), "GenericNode1" },
        { typeof(PlainRelationship), nameof(PlainRelationship) },
    };

    public static TheoryData<string, string> PropertyLabelCases => new()
    {
        { nameof(PropertyLabelOwner.DefaultName), nameof(PropertyLabelOwner.DefaultName) },
        { nameof(PropertyLabelOwner.CustomName), "custom_name" },
        { nameof(PropertyLabelOwner.EmptyLabelFallsBack), nameof(PropertyLabelOwner.EmptyLabelFallsBack) },
        { nameof(PropertyLabelOwner.Ignored), nameof(PropertyLabelOwner.Ignored) },
    };

    [Theory]
    [InlineData(null, null)]
    [InlineData("Person", "Person")]
    public void NodeAttribute_Label_ReflectsConstructorAndDefault(string? label, string? expected)
    {
        var attribute = label is null ? new NodeAttribute() : new NodeAttribute(label);

        Assert.Equal(expected, attribute.Label);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("CONNECTS", "CONNECTS")]
    public void RelationshipAttribute_Label_ReflectsConstructorAndDefault(string? label, string? expected)
    {
        var attribute = label is null ? new RelationshipAttribute() : new RelationshipAttribute(label);

        Assert.Equal(expected, attribute.Label);
    }

    [Theory]
    [MemberData(nameof(TypeLabelCases))]
    public void Labels_GetLabelFromType_UsesAttributesThenTypeNameFallback(Type type, string expected)
    {
        Assert.Equal(expected, Labels.GetLabelFromType(type));
    }

    [Theory]
    [MemberData(nameof(PropertyLabelCases))]
    public void Labels_GetLabelFromProperty_UsesPropertyAttributeThenPropertyNameFallback(string propertyName, string expected)
    {
        var property = typeof(PropertyLabelOwner).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.Equal(expected, Labels.GetLabelFromProperty(property));
    }

    [Fact]
    public void PropertyAttribute_DefaultsReflectCurrentConfiguration()
    {
        var attribute = new PropertyAttribute();

        Assert.Null(attribute.Label);
        Assert.False(attribute.Ignore);
        Assert.False(attribute.IsKey);
        Assert.False(attribute.IsIndexed);
        Assert.False(attribute.IsUnique);
        Assert.False(attribute.IsRequired);
        Assert.True(attribute.IncludeInFullTextSearch);
        Assert.Equal(int.MinValue, attribute.MinLength);
        Assert.Equal(int.MaxValue, attribute.MaxLength);
        Assert.Equal(string.Empty, attribute.Pattern);
    }

    [Theory]
    [InlineData(false, false, false, false, false, true)]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, false, false, false, true, true)]
    [InlineData(true, true, true, true, true, false)]
    [InlineData(false, true, true, false, true, false)]
    public void PropertyAttribute_StoresFlagCombinations(
        bool isKey,
        bool isIndexed,
        bool isUnique,
        bool isRequired,
        bool ignore,
        bool includeInFullTextSearch)
    {
        var attribute = new PropertyAttribute
        {
            IsKey = isKey,
            IsIndexed = isIndexed,
            IsUnique = isUnique,
            IsRequired = isRequired,
            Ignore = ignore,
            IncludeInFullTextSearch = includeInFullTextSearch,
        };

        Assert.Equal(isKey, attribute.IsKey);
        Assert.Equal(isIndexed, attribute.IsIndexed);
        Assert.Equal(isUnique, attribute.IsUnique);
        Assert.Equal(isRequired, attribute.IsRequired);
        Assert.Equal(ignore, attribute.Ignore);
        Assert.Equal(includeInFullTextSearch, attribute.IncludeInFullTextSearch);
    }

    private sealed record PlainNode : Node;

    [Node("CoreAttributedNode")]
    private sealed record AttributedNode : Node;

    private sealed record GenericNode<T> : Node;

    [Relationship("CORE_ATTRIBUTED_REL")]
    private sealed record AttributedRelationship(string Start, string End) : Relationship(Start, End);

    private sealed record PlainRelationship(string Start, string End) : Relationship(Start, End);

    private sealed class PropertyLabelOwner
    {
        public string DefaultName { get; init; } = string.Empty;

        [Property(Label = "custom_name")]
        public string CustomName { get; init; } = string.Empty;

        [Property(Label = "")]
        public string EmptyLabelFallsBack { get; init; } = string.Empty;

        [Property(Ignore = true)]
        public string Ignored { get; init; } = string.Empty;
    }
}

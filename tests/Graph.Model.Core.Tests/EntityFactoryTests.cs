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
using Cvoya.Graph.Model.Serialization;


[Trait("Area", "EntityFactory")]
public class EntityFactoryTests
{
    private static readonly DateTime FixedCreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateOnly FixedDate = new(2026, 1, 2);
    private static readonly TimeOnly FixedTime = new(3, 4, 5);
    private static readonly Point FixedPoint = new() { Longitude = 1.5, Latitude = 2.5, Height = 3.5 };

    public static TheoryData<Type, bool> CanDeserializeCases => new()
    {
        { typeof(FactoryNode), true },
        { typeof(DynamicNode), true },
        { typeof(DynamicRelationship), true },
        { typeof(UnregisteredNode), true },
        { typeof(string), false },
        { typeof(FactoryAddress), false },
    };

    static EntityFactoryTests()
    {
        EntitySerializerRegistry.Instance.Register<FactoryNode>(new FactoryNodeSerializer());
    }

    [Fact]
    public void StrongNode_RoundTripsThroughRegisteredSerializer()
    {
        var factory = new EntityFactory();
        var node = new FactoryNode
        {
            Id = "node-1",
            Labels = ["FactoryNode", "RuntimeLabel"],
            Name = "Ada",
            OptionalNote = null,
            Tags = ["engineer", "mathematician"],
            Kind = FactoryKind.Primary,
            CreatedAt = FixedCreatedAt,
            WorkDate = FixedDate,
            WorkTime = FixedTime,
            Location = FixedPoint,
            HomeAddress = new FactoryAddress("1 Main", "London"),
            Offices = [new FactoryAddress("2 Side", "Paris"), new FactoryAddress("3 High", "Berlin")],
        };

        var entityInfo = factory.Serialize(node);
        var roundTripped = factory.Deserialize<FactoryNode>(entityInfo);

        Assert.Equal(typeof(FactoryNode), entityInfo.ActualType);
        Assert.Equal("FactoryNode", entityInfo.Label);
        Assert.Equal(new[] { "FactoryNode", "RuntimeLabel" }, entityInfo.ActualLabels);
        Assert.Equal(node.Id, roundTripped.Id);
        Assert.Equal(node.Labels, roundTripped.Labels);
        Assert.Equal(node.Name, roundTripped.Name);
        Assert.Null(roundTripped.OptionalNote);
        Assert.Equal(node.Tags, roundTripped.Tags);
        Assert.Equal(node.Kind, roundTripped.Kind);
        Assert.Equal(node.CreatedAt, roundTripped.CreatedAt);
        Assert.Equal(node.WorkDate, roundTripped.WorkDate);
        Assert.Equal(node.WorkTime, roundTripped.WorkTime);
        Assert.Equal(node.Location, roundTripped.Location);
        Assert.Equal(node.HomeAddress, roundTripped.HomeAddress);
        Assert.Equal(node.Offices, roundTripped.Offices);
    }

    [Theory]
    [MemberData(nameof(CanDeserializeCases))]
    public void CanDeserialize_ReturnsCurrentCapability(Type type, bool expected)
    {
        var factory = new EntityFactory();

        Assert.Equal(expected, factory.CanDeserialize(type));
    }

    [Fact]
    public void GetSchema_ReturnsRegisteredSerializerSchema()
    {
        var factory = new EntityFactory();

        var schema = factory.GetSchema(typeof(FactoryNode));

        Assert.NotNull(schema);
        Assert.Equal(typeof(FactoryNode), schema.ExpectedType);
        Assert.Equal("FactoryNode", schema.Label);
        Assert.Contains(nameof(FactoryNode.Name), schema.SimpleProperties.Keys);
        Assert.Contains(nameof(FactoryNode.HomeAddress), schema.ComplexProperties.Keys);
        Assert.Contains(nameof(FactoryNode.Offices), schema.ComplexProperties.Keys);
    }

    [Fact]
    public void GetSchema_CachesSchemaInstances()
    {
        var factory = new EntityFactory();

        var first = factory.GetSchema(typeof(FactoryNode));
        var second = factory.GetSchema(typeof(FactoryNode));

        Assert.Same(first, second);
    }

    [Fact]
    public void Serialize_UnregisteredStrongEntity_ThrowsGraphException()
    {
        var factory = new EntityFactory();

        var exception = Assert.Throws<GraphException>(() => factory.Serialize(new UnregisteredNode()));

        Assert.Contains(nameof(UnregisteredNode), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicNode_RoundTripsSimpleValuesCollectionsAndLabels()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-1",
            ["Person", "Employee"],
            new Dictionary<string, object?>
            {
                ["name"] = "Ada",
                ["score"] = 99,
                ["kind"] = FactoryKind.Primary,
                ["createdAt"] = FixedCreatedAt,
                ["workDate"] = FixedDate,
                ["workTime"] = FixedTime,
                ["location"] = FixedPoint,
                ["tags"] = new[] { "engineer", "mathematician" },
                ["optional"] = null,
            });

        var entityInfo = factory.Serialize(node);
        var roundTripped = factory.Deserialize<DynamicNode>(entityInfo);

        Assert.Equal("Person", entityInfo.Label);
        Assert.Equal(new[] { "Person", "Employee" }, entityInfo.ActualLabels);
        Assert.Equal("dynamic-1", roundTripped.Id);
        Assert.Equal(node.Labels, roundTripped.Labels);
        Assert.Equal("Ada", roundTripped.Properties["name"]);
        Assert.Equal(99, roundTripped.Properties["score"]);
        Assert.Equal(FactoryKind.Primary, roundTripped.Properties["kind"]);
        Assert.Equal(FixedCreatedAt, roundTripped.Properties["createdAt"]);
        Assert.Equal(FixedDate, roundTripped.Properties["workDate"]);
        Assert.Equal(FixedTime, roundTripped.Properties["workTime"]);
        Assert.Equal(FixedPoint, roundTripped.Properties["location"]);
        Assert.Null(roundTripped.Properties["optional"]);
    }

    [Fact]
    public void DynamicNode_RoundTripsDictionaryComplexPropertyAsDictionary()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-1",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["address"] = new Dictionary<string, object?>
                {
                    ["street"] = "1 Main",
                    ["city"] = "London",
                    ["unit"] = 10,
                },
            });

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        var address = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["address"]);
        Assert.Equal("1 Main", address["street"]);
        Assert.Equal("London", address["city"]);
        Assert.Equal(10, address["unit"]);
    }

    [Fact]
    public void DynamicRelationship_RoundTripPreservesShapeAndCharacterizesCurrentIdLoss()
    {
        var factory = new EntityFactory();
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            new Dictionary<string, object?> { ["since"] = 2024 })
        {
            Id = "rel-1",
        };

        var entityInfo = factory.Serialize(relationship);
        var roundTripped = factory.Deserialize<DynamicRelationship>(entityInfo);

        Assert.Equal("KNOWS", entityInfo.Label);
        Assert.Equal("source", roundTripped.StartNodeId);
        Assert.Equal("target", roundTripped.EndNodeId);
        Assert.Equal("KNOWS", roundTripped.Type);
        Assert.Equal(2024, roundTripped.Properties["since"]);
        Assert.NotEqual("rel-1", roundTripped.Id); // Characterizes https://github.com/cvoya-com/graphmodel-dotnet/issues/125.
    }

    private sealed record FactoryNode : Node
    {
        public string Name { get; init; } = string.Empty;

        public string? OptionalNote { get; init; }

        public List<string> Tags { get; init; } = new();

        public FactoryKind Kind { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateOnly WorkDate { get; init; }

        public TimeOnly WorkTime { get; init; }

        public Point Location { get; init; }

        public FactoryAddress? HomeAddress { get; init; }

        public List<FactoryAddress> Offices { get; init; } = new();
    }

    private sealed record UnregisteredNode : Node;

    private sealed record FactoryAddress(string Street, string City);

    private enum FactoryKind
    {
        Unknown,
        Primary,
    }

    private sealed class FactoryNodeSerializer : IEntitySerializer
    {
        public Type EntityType => typeof(FactoryNode);

        public EntityInfo Serialize(object obj)
        {
            var node = (FactoryNode)obj;
            var simpleProperties = new Dictionary<string, Property>
            {
                [nameof(FactoryNode.Id)] = SimpleProperty(typeof(Node), nameof(Node.Id), node.Id, typeof(string)),
                [nameof(FactoryNode.Labels)] = CollectionProperty(typeof(Node), nameof(Node.Labels), node.Labels, typeof(string)),
                [nameof(FactoryNode.Name)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.Name), node.Name, typeof(string)),
                [nameof(FactoryNode.OptionalNote)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.OptionalNote), node.OptionalNote, typeof(string)),
                [nameof(FactoryNode.Tags)] = CollectionProperty(typeof(FactoryNode), nameof(FactoryNode.Tags), node.Tags, typeof(string)),
                [nameof(FactoryNode.Kind)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.Kind), node.Kind, typeof(FactoryKind)),
                [nameof(FactoryNode.CreatedAt)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.CreatedAt), node.CreatedAt, typeof(DateTime)),
                [nameof(FactoryNode.WorkDate)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.WorkDate), node.WorkDate, typeof(DateOnly)),
                [nameof(FactoryNode.WorkTime)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.WorkTime), node.WorkTime, typeof(TimeOnly)),
                [nameof(FactoryNode.Location)] = SimpleProperty(typeof(FactoryNode), nameof(FactoryNode.Location), node.Location, typeof(Point)),
            };

            var complexProperties = new Dictionary<string, Property>();

            if (node.HomeAddress is not null)
            {
                complexProperties[nameof(FactoryNode.HomeAddress)] = new Property(
                    GetProperty(typeof(FactoryNode), nameof(FactoryNode.HomeAddress)),
                    nameof(FactoryNode.HomeAddress),
                    true,
                    SerializeAddress(node.HomeAddress));
            }

            complexProperties[nameof(FactoryNode.Offices)] = new Property(
                GetProperty(typeof(FactoryNode), nameof(FactoryNode.Offices)),
                nameof(FactoryNode.Offices),
                false,
                new EntityCollection(
                    typeof(FactoryAddress),
                    node.Offices.Select(SerializeAddress).ToList()));

            var labels = node.Labels.ToList();
            var primaryLabel = labels.Count > 0 ? labels[0] : "FactoryNode";

            return new EntityInfo(
                typeof(FactoryNode),
                primaryLabel,
                labels,
                simpleProperties,
                complexProperties);
        }

        public object Deserialize(EntityInfo entity)
        {
            var node = new FactoryNode
            {
                Id = ReadSimple<string>(entity, nameof(FactoryNode.Id)) ?? string.Empty,
                Labels = ReadSimpleCollection<string>(entity, nameof(FactoryNode.Labels)),
                Name = ReadSimple<string>(entity, nameof(FactoryNode.Name)) ?? string.Empty,
                OptionalNote = ReadSimple<string>(entity, nameof(FactoryNode.OptionalNote)),
                Tags = ReadSimpleCollection<string>(entity, nameof(FactoryNode.Tags)),
                Kind = ReadSimple<FactoryKind>(entity, nameof(FactoryNode.Kind)),
                CreatedAt = ReadSimple<DateTime>(entity, nameof(FactoryNode.CreatedAt)),
                WorkDate = ReadSimple<DateOnly>(entity, nameof(FactoryNode.WorkDate)),
                WorkTime = ReadSimple<TimeOnly>(entity, nameof(FactoryNode.WorkTime)),
                Location = ReadSimple<Point>(entity, nameof(FactoryNode.Location)),
                HomeAddress = ReadAddress(entity, nameof(FactoryNode.HomeAddress)),
                Offices = ReadAddressCollection(entity, nameof(FactoryNode.Offices)),
            };

            return node;
        }

        public EntitySchema GetSchema()
        {
            var simpleProperties = new Dictionary<string, PropertySchema>
            {
                [nameof(FactoryNode.Id)] = SimpleSchema(typeof(Node), nameof(Node.Id), typeof(string)),
                [nameof(FactoryNode.Labels)] = SimpleCollectionSchema(typeof(Node), nameof(Node.Labels), typeof(string)),
                [nameof(FactoryNode.Name)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.Name), typeof(string)),
                [nameof(FactoryNode.OptionalNote)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.OptionalNote), typeof(string), true),
                [nameof(FactoryNode.Tags)] = SimpleCollectionSchema(typeof(FactoryNode), nameof(FactoryNode.Tags), typeof(string)),
                [nameof(FactoryNode.Kind)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.Kind), typeof(FactoryKind)),
                [nameof(FactoryNode.CreatedAt)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.CreatedAt), typeof(DateTime)),
                [nameof(FactoryNode.WorkDate)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.WorkDate), typeof(DateOnly)),
                [nameof(FactoryNode.WorkTime)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.WorkTime), typeof(TimeOnly)),
                [nameof(FactoryNode.Location)] = SimpleSchema(typeof(FactoryNode), nameof(FactoryNode.Location), typeof(Point)),
            };

            var addressSchema = AddressSchema();
            var complexProperties = new Dictionary<string, PropertySchema>
            {
                [nameof(FactoryNode.HomeAddress)] = new(
                    GetProperty(typeof(FactoryNode), nameof(FactoryNode.HomeAddress)),
                    nameof(FactoryNode.HomeAddress),
                    PropertyType.Complex,
                    IsNullable: true,
                    NestedSchema: addressSchema),
                [nameof(FactoryNode.Offices)] = new(
                    GetProperty(typeof(FactoryNode), nameof(FactoryNode.Offices)),
                    nameof(FactoryNode.Offices),
                    PropertyType.ComplexCollection,
                    ElementType: typeof(FactoryAddress),
                    NestedSchema: addressSchema),
            };

            return new EntitySchema(
                typeof(FactoryNode),
                "FactoryNode",
                false,
                false,
                simpleProperties,
                complexProperties);
        }

        private static EntityInfo SerializeAddress(FactoryAddress address)
        {
            return new EntityInfo(
                typeof(FactoryAddress),
                "FactoryAddress",
                [],
                new Dictionary<string, Property>
                {
                    [nameof(FactoryAddress.Street)] = SimpleProperty(typeof(FactoryAddress), nameof(FactoryAddress.Street), address.Street, typeof(string)),
                    [nameof(FactoryAddress.City)] = SimpleProperty(typeof(FactoryAddress), nameof(FactoryAddress.City), address.City, typeof(string)),
                },
                new Dictionary<string, Property>());
        }

        private static FactoryAddress? ReadAddress(EntityInfo entity, string propertyName)
        {
            if (!entity.ComplexProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not EntityInfo addressInfo)
            {
                return null;
            }

            return new FactoryAddress(
                ReadSimple<string>(addressInfo, nameof(FactoryAddress.Street)) ?? string.Empty,
                ReadSimple<string>(addressInfo, nameof(FactoryAddress.City)) ?? string.Empty);
        }

        private static List<FactoryAddress> ReadAddressCollection(EntityInfo entity, string propertyName)
        {
            if (!entity.ComplexProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not EntityCollection collection)
            {
                return new List<FactoryAddress>();
            }

            return collection.Entities
                .Select(addressInfo => new FactoryAddress(
                    ReadSimple<string>(addressInfo, nameof(FactoryAddress.Street)) ?? string.Empty,
                    ReadSimple<string>(addressInfo, nameof(FactoryAddress.City)) ?? string.Empty))
                .ToList();
        }

        private static T? ReadSimple<T>(EntityInfo entity, string propertyName)
        {
            if (!entity.SimpleProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not SimpleValue simpleValue ||
                simpleValue.Object is null)
            {
                return default;
            }

            return (T)simpleValue.Object;
        }

        private static List<T> ReadSimpleCollection<T>(EntityInfo entity, string propertyName)
        {
            if (!entity.SimpleProperties.TryGetValue(propertyName, out var property) ||
                property.Value is not SimpleCollection simpleCollection)
            {
                return new List<T>();
            }

            return simpleCollection.Values.Select(value => (T)value.Object).ToList();
        }

        private static Property SimpleProperty(Type declaringType, string name, object? value, Type valueType)
        {
            var serializedValue = value is null ? null : new SimpleValue(value, valueType);

            return new Property(
                GetProperty(declaringType, name),
                name,
                value is null,
                serializedValue);
        }

        private static Property CollectionProperty<T>(Type declaringType, string name, IEnumerable<T> values, Type elementType)
        {
            return new Property(
                GetProperty(declaringType, name),
                name,
                false,
                new SimpleCollection(
                    values.Select(value => new SimpleValue(value!, elementType)).ToList(),
                    elementType));
        }

        private static PropertySchema SimpleSchema(Type declaringType, string name, Type type, bool isNullable = false)
        {
            _ = type;

            return new PropertySchema(
                GetProperty(declaringType, name),
                name,
                PropertyType.Simple,
                IsNullable: isNullable);
        }

        private static PropertySchema SimpleCollectionSchema(Type declaringType, string name, Type elementType)
        {
            return new PropertySchema(
                GetProperty(declaringType, name),
                name,
                PropertyType.SimpleCollection,
                ElementType: elementType);
        }

        private static EntitySchema AddressSchema()
        {
            return new EntitySchema(
                typeof(FactoryAddress),
                "FactoryAddress",
                false,
                false,
                new Dictionary<string, PropertySchema>
                {
                    [nameof(FactoryAddress.Street)] = SimpleSchema(typeof(FactoryAddress), nameof(FactoryAddress.Street), typeof(string)),
                    [nameof(FactoryAddress.City)] = SimpleSchema(typeof(FactoryAddress), nameof(FactoryAddress.City), typeof(string)),
                },
                new Dictionary<string, PropertySchema>());
        }

        private static PropertyInfo GetProperty(Type type, string name)
        {
            return type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Property {type.Name}.{name} was not found.");
        }
    }
}

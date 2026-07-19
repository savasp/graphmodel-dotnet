// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using Cvoya.Graph.Serialization;


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

    private static readonly string[] ExpectedLabels = ["FactoryNode", "RuntimeLabel"];
    private static readonly string[] DynamicLabels = ["Person", "Employee"];
    private static readonly string[] DynamicTags = ["engineer", "mathematician"];
    private static readonly string[] ExpectedDynamicStrings = ["first", "second"];
    private static readonly int[] ExpectedDynamicIntegers = [1, 2, 3];
    private static readonly FactoryKind[] ExpectedDynamicKinds = [FactoryKind.Unknown, FactoryKind.Primary];
    private static readonly int?[] ExpectedDynamicNullableIntegers = [1, null, 3];
    private static readonly Guid[] DynamicIds =
    [
        Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963"),
        Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f"),
    ];

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
        Assert.Equal(ExpectedLabels, entityInfo.ActualLabels);
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
                ["tags"] = DynamicTags,
                ["optional"] = null,
            });

        var entityInfo = factory.Serialize(node);
        var roundTripped = factory.Deserialize<DynamicNode>(entityInfo);

        Assert.Equal("Person", entityInfo.Label);
        Assert.Equal(DynamicLabels, entityInfo.ActualLabels);
        Assert.Equal("dynamic-1", roundTripped.Id);
        Assert.Equal(node.Labels, roundTripped.Labels);
        Assert.Equal("Ada", roundTripped.Properties["name"]);
        Assert.Equal(99, roundTripped.Properties["score"]);
        Assert.Equal(FactoryKind.Primary, roundTripped.Properties["kind"]);
        Assert.Equal(FixedCreatedAt, roundTripped.Properties["createdAt"]);
        Assert.Equal(FixedDate, roundTripped.Properties["workDate"]);
        Assert.Equal(FixedTime, roundTripped.Properties["workTime"]);
        Assert.Equal(FixedPoint, roundTripped.Properties["location"]);
        Assert.Equal(DynamicTags, Assert.IsType<List<string>>(roundTripped.Properties["tags"]));
        Assert.Null(roundTripped.Properties["optional"]);
    }

    [Fact]
    public void DynamicNode_RoundTripsTypedSimpleCollectionsAsLists()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode("dynamic-collections", ["Person"], CreateDynamicCollectionProperties());

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        AssertDynamicCollectionProperties(roundTripped.Properties);
    }

    [Fact]
    public void DynamicRelationship_RoundTripsTypedSimpleCollectionsAsLists()
    {
        var factory = new EntityFactory();
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            CreateDynamicCollectionProperties());

        var roundTripped = factory.Deserialize<DynamicRelationship>(factory.Serialize(relationship));

        AssertDynamicCollectionProperties(roundTripped.Properties);
    }

    [Fact]
    public void DynamicCollection_WithMalformedItem_ThrowsGraphException()
    {
        var factory = new EntityFactory();
        var entity = factory.Serialize(new DynamicNode("dynamic-invalid", ["Person"], new Dictionary<string, object?>()));
        entity.SimpleProperties["invalid"] = new Property(
            PropertyInfo: null!,
            Label: "invalid",
            IsNullable: false,
            Value: new SimpleCollection(
                [new SimpleValue("not-an-integer", typeof(string))],
                typeof(int)));

        var exception = Assert.Throws<GraphException>(() => factory.Deserialize<DynamicNode>(entity));

        Assert.Contains("invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(int).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicCollection_WithNullElementForNonNullableValueType_ThrowsGraphException()
    {
        var factory = new EntityFactory();
        var entity = factory.Serialize(new DynamicNode("dynamic-null-element", ["Person"], new Dictionary<string, object?>()));
        entity.SimpleProperties["invalid"] = new Property(
            PropertyInfo: null!,
            Label: "invalid",
            IsNullable: false,
            Value: new SimpleCollection([new SimpleValue(null!, typeof(int))], typeof(int)));

        var exception = Assert.Throws<GraphException>(() => factory.Deserialize<DynamicNode>(entity));

        Assert.Contains("invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null element", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(int).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicProperty_WithNullSerializedValue_DeserializesAsNull()
    {
        var factory = new EntityFactory();
        var entity = factory.Serialize(new DynamicNode("dynamic-null-value", ["Person"], new Dictionary<string, object?>()));
        entity.SimpleProperties["optional"] = new Property(
            PropertyInfo: null!,
            Label: "optional",
            IsNullable: true,
            Value: null);

        var roundTripped = factory.Deserialize<DynamicNode>(entity);

        Assert.True(roundTripped.Properties.ContainsKey("optional"));
        Assert.Null(roundTripped.Properties["optional"]);
    }

    [Fact]
    public void ComplexValueSimpleCollections_PreserveNullElementsAndDeclaredElementType()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-complex-collections",
            ["Person"],
            new Dictionary<string, object?> { ["survey"] = new FactorySurvey() });

        var entity = factory.Serialize(node);

        var complex = Assert.IsType<EntityInfo>(entity.ComplexProperties["survey"].Value);
        var scores = Assert.IsType<SimpleCollection>(complex.SimpleProperties[nameof(FactorySurvey.Scores)].Value);
        Assert.Equal(typeof(int?), scores.ElementType);
        Assert.Equal(new object?[] { 1, null, 3 }, scores.Values.Select(value => value.Object));
        Assert.All(scores.Values, value => Assert.Equal(typeof(int?), value.Type));
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
    public void DynamicNode_RoundTripsCollectionOfPocoComplexProperty()
    {
        // Exercises the #121 fix: GraphDataModel.IsCollectionOfComplex now correctly identifies a
        // List<POCO> dynamic property value as a collection-of-complex, so EntityFactory serializes
        // each element as its own EntityInfo instead of falling through to treating the List<T>
        // instance itself as a single complex object.
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-1",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["addresses"] = new List<FactoryAddress>
                {
                    new("1 Main", "London"),
                    new("2 Side", "Paris"),
                },
            });

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        var addresses = Assert.IsAssignableFrom<List<Dictionary<string, object?>>>(roundTripped.Properties["addresses"]);
        Assert.Equal(2, addresses.Count);
        Assert.Equal("1 Main", addresses[0]["Street"]);
        Assert.Equal("London", addresses[0]["City"]);
        Assert.Equal("2 Side", addresses[1]["Street"]);
        Assert.Equal("Paris", addresses[1]["City"]);
    }

    [Fact]
    public void DynamicRelationship_RoundTripPreservesId()
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
        Assert.Equal("rel-1", roundTripped.Id);
    }

    [Fact]
    public void DynamicRelationship_RoundTripsPocoComplexPropertyWithCustomLabels()
    {
        var factory = new EntityFactory();
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            new Dictionary<string, object?> { ["contact"] = new LabeledContact { DisplayName = "Ada" } });

        var entityInfo = factory.Serialize(relationship);
        var roundTripped = factory.Deserialize<DynamicRelationship>(entityInfo);

        // The serialized representation is keyed by the physical property label...
        var contactInfo = Assert.IsType<EntityInfo>(entityInfo.ComplexProperties["contact"].Value);
        Assert.True(contactInfo.SimpleProperties.ContainsKey("display_name"));
        Assert.False(contactInfo.SimpleProperties.ContainsKey(nameof(LabeledContact.DisplayName)));

        // ...and rehydration produces the canonical dynamic shape - a dictionary keyed by that same
        // stored label. A dynamic entity has no POCO to materialize into, and this shape matches what
        // a node produces for the same stored value (see DynamicNodeAndRelationship_* below).
        var contact = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["contact"]);
        Assert.Equal("Ada", contact["display_name"]);
        Assert.False(contact.ContainsKey(nameof(LabeledContact.DisplayName)));
    }

    [Fact]
    public void DynamicNode_RoundTripPreservesId()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "node-1",
            ["Person"],
            new Dictionary<string, object?> { ["name"] = "Ada" });

        var entityInfo = factory.Serialize(node);
        var roundTripped = factory.Deserialize<DynamicNode>(entityInfo);

        Assert.Equal("node-1", roundTripped.Id);
    }

    [Fact]
    public void DynamicNode_SerializesNestedDictionaryCollectionAsElementsNotReflectedProperties()
    {
        // The reported #405 failure: a collection nested inside a dictionary property value fell into
        // the "else complex" branch and was reflected over as an opaque object, serializing the
        // collection class's Length/Rank/... instead of its elements. It must serialize as a
        // SimpleCollection of the elements.
        var factory = new EntityFactory();
        var expectedTags = new[] { "a", "b" };
        var node = new DynamicNode(
            "dynamic-nested-serialize",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["address"] = new Dictionary<string, object?>
                {
                    ["tags"] = expectedTags,
                },
            });

        var entity = factory.Serialize(node);

        var address = Assert.IsType<EntityInfo>(entity.ComplexProperties["address"].Value);
        var tags = Assert.IsType<SimpleCollection>(address.SimpleProperties["tags"].Value);
        Assert.Equal(typeof(string), tags.ElementType);
        Assert.Equal(new object?[] { "a", "b" }, tags.Values.Select(value => value.Object));
        // The reflected-collection failure mode would have surfaced members like Length/Rank instead.
        Assert.DoesNotContain("Length", address.SimpleProperties.Keys);
        Assert.Empty(address.ComplexProperties);
    }

    [Fact]
    public void DynamicNode_RoundTripsSimpleCollectionsNestedInDictionaryProperty()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-nested-collections",
            ["Person"],
            new Dictionary<string, object?> { ["bag"] = CreateDynamicCollectionProperties() });

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        var bag = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["bag"]);
        AssertDynamicCollectionProperties(bag);
    }

    [Fact]
    public void DynamicRelationship_RoundTripsSimpleCollectionsNestedInDictionaryProperty()
    {
        var factory = new EntityFactory();
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            new Dictionary<string, object?> { ["bag"] = CreateDynamicCollectionProperties() });

        var roundTripped = factory.Deserialize<DynamicRelationship>(factory.Serialize(relationship));

        var bag = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["bag"]);
        AssertDynamicCollectionProperties(bag);
    }

    [Fact]
    public void DynamicNodeAndRelationship_ProduceSameCanonicalShapeForNestedComplexValue()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "node-parity",
            ["Person"],
            new Dictionary<string, object?> { ["bag"] = CreateDynamicCollectionProperties() });
        var relationship = new DynamicRelationship(
            "source",
            "target",
            "KNOWS",
            new Dictionary<string, object?> { ["bag"] = CreateDynamicCollectionProperties() });

        var nodeProperties = factory.Deserialize<DynamicNode>(factory.Serialize(node)).Properties;
        var relationshipProperties = factory.Deserialize<DynamicRelationship>(factory.Serialize(relationship)).Properties;

        // Same canonical shape for the same stored value regardless of owner: both materialize the
        // complex value as a dictionary whose nested collections satisfy identical assertions.
        var nodeBag = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(nodeProperties["bag"]);
        var relationshipBag = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(relationshipProperties["bag"]);
        AssertDynamicCollectionProperties(nodeBag);
        AssertDynamicCollectionProperties(relationshipBag);
    }

    [Fact]
    public void DynamicNode_ReconstructsSimpleCollectionNestedInPocoComplexProperty()
    {
        // The node deserialize path previously copied only nested SimpleValue members into the
        // dictionary and dropped nested SimpleCollection members. A POCO complex value carrying a
        // simple collection must round-trip it as a List<T>.
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-poco-collection",
            ["Person"],
            new Dictionary<string, object?> { ["survey"] = new FactorySurvey() });

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        var survey = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["survey"]);
        var scores = Assert.IsType<List<int?>>(survey[nameof(FactorySurvey.Scores)]);
        Assert.Equal(new int?[] { 1, null, 3 }, scores);
    }

    [Fact]
    public void DynamicNode_RoundTripsSimpleCollectionNestedInDictionaryWithinDictionary()
    {
        var factory = new EntityFactory();
        var expectedTags = new[] { "x", "y", "z" };
        var node = new DynamicNode(
            "dynamic-deep-nested",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["outer"] = new Dictionary<string, object?>
                {
                    ["inner"] = new Dictionary<string, object?>
                    {
                        ["tags"] = expectedTags,
                    },
                },
            });

        var roundTripped = factory.Deserialize<DynamicNode>(factory.Serialize(node));

        var outer = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["outer"]);
        var inner = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(outer["inner"]);
        var tags = Assert.IsType<List<string>>(inner["tags"]);
        Assert.Equal(expectedTags, tags);
    }

    [Fact]
    public void DynamicNode_RoundTripsCollectionOfDictionariesNestedInDictionaryProperty()
    {
        var factory = new EntityFactory();
        var firstTags = new[] { "a", "b" };
        var secondTags = new[] { "c" };
        var node = new DynamicNode(
            "dynamic-dictionary-collection",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["bag"] = new Dictionary<string, object?>
                {
                    ["entries"] = new List<Dictionary<string, object?>>
                    {
                        new() { ["name"] = "first", ["tags"] = firstTags },
                        new() { ["name"] = "second", ["tags"] = secondTags },
                    },
                },
            });

        var entity = factory.Serialize(node);

        var bagInfo = Assert.IsType<EntityInfo>(entity.ComplexProperties["bag"].Value);
        var entriesInfo = Assert.IsType<EntityCollection>(bagInfo.ComplexProperties["entries"].Value);
        Assert.Equal(2, entriesInfo.Entities.Count);
        Assert.All(entriesInfo.Entities, entry => Assert.DoesNotContain("Count", entry.SimpleProperties.Keys));

        var roundTripped = factory.Deserialize<DynamicNode>(entity);
        var bag = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(roundTripped.Properties["bag"]);
        var entries = Assert.IsType<List<Dictionary<string, object?>>>(bag["entries"]);
        Assert.Equal(["first", "second"], entries.Select(entry => entry["name"]));
        Assert.Equal(firstTags, Assert.IsType<List<string>>(entries[0]["tags"]));
        Assert.Equal(secondTags, Assert.IsType<List<string>>(entries[1]["tags"]));
    }

    [Fact]
    public void DynamicNode_WithNullComplexCollectionElement_ThrowsIndexedDiagnostic()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-null-complex",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["stored_addresses"] = new List<FactoryAddress?>
                {
                    new("First", "Seattle"),
                    null,
                },
            });

        var exception = Assert.Throws<GraphException>(() => factory.Serialize(node));

        Assert.Equal(
            "Complex collection property 'stored_addresses' contains a null element at index 1, " +
            $"but its target element type '{typeof(FactoryAddress)}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void DynamicNode_WithNullComplexCollectionNestedInPoco_ThrowsIndexedDiagnostic()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-nested-null-complex",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["directory"] = new FactoryDirectory
                {
                    Offices = [new("First", "Seattle"), null],
                },
            });

        var exception = Assert.Throws<GraphException>(() => factory.Serialize(node));

        Assert.Equal(
            "Complex collection property 'stored_offices' contains a null element at index 1, " +
            $"but its target element type '{typeof(FactoryAddress)}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void DynamicNode_WithNullDictionaryCollectionElement_ThrowsIndexedDiagnostic()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-null-dictionary",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["entries"] = new List<Dictionary<string, object?>?>
                {
                    new() { ["name"] = "first" },
                    null,
                },
            });

        var exception = Assert.Throws<GraphException>(() => factory.Serialize(node));

        Assert.Equal(
            "Complex collection property 'entries' contains a null element at index 1, " +
            $"but its target element type '{typeof(Dictionary<string, object?>)}' does not allow null elements.",
            exception.Message);
    }

    [Fact]
    public void DynamicNode_WithMistypedStoredComplexElement_ThrowsIndexedDiagnostic()
    {
        var factory = new EntityFactory();
        var entity = factory.Serialize(new DynamicNode(
            "dynamic-mistyped-complex",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["stored_addresses"] = new List<FactoryAddress>
                {
                    new("First", "Seattle"),
                },
            }));
        var property = entity.ComplexProperties["stored_addresses"];
        var collection = Assert.IsType<EntityCollection>(property.Value);
        var mistyped = collection.Entities.Single() with { ActualType = typeof(FactorySurvey) };
        entity.ComplexProperties["stored_addresses"] = property with
        {
            Value = new EntityCollection(collection.Type, [mistyped]),
        };

        var exception = Assert.Throws<GraphException>(() => factory.Deserialize<DynamicNode>(entity));

        Assert.Equal(
            $"Complex collection property 'stored_addresses' contains an element of type '{typeof(FactorySurvey)}' at index 0, " +
            $"which is not assignable to its target element type '{typeof(FactoryAddress)}'.",
            exception.Message);
    }

    [Fact]
    public void DynamicNode_WithDerivedComplexCollectionElement_PreservesRuntimeTypeAndProperties()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-derived-complex",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["locations"] = new List<FactoryLocation>
                {
                    new FactoryOffice("Seattle", 14),
                },
            });

        var entity = factory.Serialize(node);

        var collection = Assert.IsType<EntityCollection>(entity.ComplexProperties["locations"].Value);
        Assert.Equal(typeof(FactoryLocation), collection.Type);
        var storedOffice = Assert.Single(collection.Entities);
        Assert.Equal(typeof(FactoryOffice), storedOffice.ActualType);
        Assert.Equal(14, Assert.IsType<SimpleValue>(storedOffice.SimpleProperties[nameof(FactoryOffice.Floor)].Value).Object);

        var roundTripped = factory.Deserialize<DynamicNode>(entity);
        var locations = Assert.IsType<List<Dictionary<string, object?>>>(roundTripped.Properties["locations"]);
        var office = Assert.Single(locations);
        Assert.Equal("Seattle", office[nameof(FactoryLocation.Name)]);
        Assert.Equal(14, office[nameof(FactoryOffice.Floor)]);
    }

    [Fact]
    public void DynamicNode_WithDerivedComplexCollectionNestedInPoco_PreservesRuntimeTypeAndProperties()
    {
        var factory = new EntityFactory();
        var node = new DynamicNode(
            "dynamic-nested-derived-complex",
            ["Person"],
            new Dictionary<string, object?>
            {
                ["directory"] = new FactoryLocationDirectory
                {
                    Locations = [new FactoryOffice("Seattle", 14)],
                },
            });

        var entity = factory.Serialize(node);

        var directory = Assert.IsType<EntityInfo>(entity.ComplexProperties["directory"].Value);
        var collection = Assert.IsType<EntityCollection>(directory.ComplexProperties["stored_locations"].Value);
        Assert.Equal(typeof(FactoryLocation), collection.Type);
        var storedOffice = Assert.Single(collection.Entities);
        Assert.Equal(typeof(FactoryOffice), storedOffice.ActualType);
        Assert.Equal(14, Assert.IsType<SimpleValue>(storedOffice.SimpleProperties[nameof(FactoryOffice.Floor)].Value).Object);

        var roundTripped = factory.Deserialize<DynamicNode>(entity);
        var roundTrippedDirectory = Assert.IsType<Dictionary<string, object?>>(roundTripped.Properties["directory"]);
        var locations = Assert.IsType<List<Dictionary<string, object?>>>(roundTrippedDirectory["stored_locations"]);
        var office = Assert.Single(locations);
        Assert.Equal("Seattle", office[nameof(FactoryLocation.Name)]);
        Assert.Equal(14, office[nameof(FactoryOffice.Floor)]);
    }

    [Fact]
    public void DynamicNode_WithSelfReferentialDictionary_ThrowsGraphException()
    {
        var factory = new EntityFactory();
        var bag = new Dictionary<string, object?>();
        bag["self"] = bag;
        var node = new DynamicNode(
            "dynamic-dictionary-cycle",
            ["Person"],
            new Dictionary<string, object?> { ["bag"] = bag });

        var exception = Assert.Throws<GraphException>(() => factory.Serialize(node));

        Assert.Contains("Reference cycle", exception.Message, StringComparison.Ordinal);
        Assert.Contains("self", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicComplexValue_WithMalformedNestedCollection_ThrowsGraphException()
    {
        var factory = new EntityFactory();
        var entity = factory.Serialize(new DynamicNode(
            "dynamic-nested-invalid",
            ["Person"],
            new Dictionary<string, object?> { ["bag"] = new Dictionary<string, object?>() }));

        // Inject a nested simple collection with a null element for a non-nullable value type - the
        // malformed shape #404 rejects at the top level, now nested inside a complex value.
        var nested = Assert.IsType<EntityInfo>(entity.ComplexProperties["bag"].Value);
        nested.SimpleProperties["scores"] = new Property(
            PropertyInfo: null!,
            Label: "scores",
            IsNullable: false,
            Value: new SimpleCollection([new SimpleValue(null!, typeof(int))], typeof(int)));

        var exception = Assert.Throws<GraphException>(() => factory.Deserialize<DynamicNode>(entity));

        Assert.Contains("scores", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null element", exception.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> CreateDynamicCollectionProperties() => new()
    {
        ["strings"] = new[] { "first", "second" },
        ["integers"] = new List<int> { 1, 2, 3 },
        ["guids"] = DynamicIds,
        ["kinds"] = new[] { FactoryKind.Unknown, FactoryKind.Primary },
        ["nullableIntegers"] = new int?[] { 1, null, 3 },
        ["emptyIntegers"] = Array.Empty<int>(),
    };

    private static void AssertDynamicCollectionProperties(IReadOnlyDictionary<string, object?> properties)
    {
        Assert.Equal(ExpectedDynamicStrings, Assert.IsType<List<string>>(properties["strings"]));
        Assert.Equal(ExpectedDynamicIntegers, Assert.IsType<List<int>>(properties["integers"]));
        Assert.Equal(DynamicIds, Assert.IsType<List<Guid>>(properties["guids"]));
        Assert.Equal(
            ExpectedDynamicKinds,
            Assert.IsType<List<FactoryKind>>(properties["kinds"]));
        Assert.Equal(ExpectedDynamicNullableIntegers, Assert.IsType<List<int?>>(properties["nullableIntegers"]));
        Assert.Empty(Assert.IsType<List<int>>(properties["emptyIntegers"]));
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

    private sealed record FactorySurvey
    {
        public List<int?> Scores { get; init; } = [1, null, 3];
    }

    private sealed record FactoryDirectory
    {
        [Property(Label = "stored_offices")]
        public List<FactoryAddress?> Offices { get; init; } = [];
    }

    private record FactoryLocation(string Name);

    private sealed record FactoryOffice(string Name, int Floor) : FactoryLocation(Name);

    private sealed record FactoryLocationDirectory
    {
        [Property(Label = "stored_locations")]
        public List<FactoryLocation> Locations { get; init; } = [];
    }

    private sealed record LabeledContact
    {
        [Property(Label = "display_name")]
        public string DisplayName { get; set; } = string.Empty;
    }

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

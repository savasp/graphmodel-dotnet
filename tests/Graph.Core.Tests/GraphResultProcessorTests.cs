// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;

namespace Cvoya.Graph.Core.Tests;

public class GraphResultProcessorTests
{
    private static readonly int[] ExpectedIntegerList = [1, 2, 3];
    private static readonly int[] ExpectedIntegerArray = [4, 5];
    private readonly EntityFactory factory = new();

    static GraphResultProcessorTests()
    {
        EntitySerializerRegistry.Instance.Register<ValueCollectionNode>(new ValueCollectionNodeSerializer());
    }

    [Fact]
    public async Task EquivalentWireFixtures_MaterializeIdenticalDynamicNodes()
    {
        var first = NodeRecord(Node("node-1", "person-1", "Ada", 9_223_372_036_854_775_000L, 1234567890.123456789m));
        var second = NodeRecord(Node("other-driver-node-1", "person-1", "Ada", 9_223_372_036_854_775_000L, 1234567890.123456789m));
        var materializer = new GraphResultMaterializer(factory, loggerFactory: null);

        var firstNode = Assert.IsType<DynamicNode>(await materializer.MaterializeAsync<DynamicNode>(
            [first], cancellationToken: TestContext.Current.CancellationToken));
        var secondNode = Assert.IsType<DynamicNode>(await materializer.MaterializeAsync<DynamicNode>(
            [second], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(firstNode.Id, secondNode.Id);
        Assert.Equal(firstNode.Labels, secondNode.Labels);
        Assert.Equal(firstNode.Properties, secondNode.Properties);
        Assert.IsType<long>(firstNode.Properties["large"]);
        Assert.IsType<decimal>(firstNode.Properties["precise"]);
    }

    [Fact]
    public async Task ComplexPropertyWireFixture_ReassemblesOrderedDynamicCollection()
    {
        var parent = Node("parent-wire", "parent", "Parent", 1, 1m);
        var firstChild = Node("child-wire-1", "child-1", "First", 2, 2m);
        var secondChild = Node("child-wire-2", "child-2", "Second", 3, 3m);
        var firstEdge = Relationship("edge-1", "Children", parent, firstChild, sequence: 1);
        var secondEdge = Relationship("edge-2", "Children", parent, secondChild, sequence: 0);
        var record = NodeRecord(parent,
        [
            ComplexProperty(parent, firstEdge, firstChild, 1),
            ComplexProperty(parent, secondEdge, secondChild, 0),
        ]);

        var info = Assert.Single(await new GraphResultProcessor(factory).ProcessAsync(
            [record], typeof(DynamicNode), TestContext.Current.CancellationToken));
        var node = Assert.IsType<DynamicNode>(factory.Deserialize(info));
        var children = Assert.IsType<List<Dictionary<string, object?>>>(node.Properties["Children"]);

        Assert.Equal(["Second", "First"], children.Select(child => child["name"]));
    }

    [Fact]
    public async Task RelationshipWireFixture_ReconstructsPublicEndpointIds()
    {
        var start = Node("start-wire", "start-public", "Start", 1, 1m);
        var end = Node("end-wire", "end-public", "End", 2, 2m);
        var relationship = Relationship("edge-wire", "KNOWS", start, end, sequence: 0);
        var record = new GraphRecord(new Dictionary<string, GraphValue>
        {
            ["PathSegment"] = PathSegment(start, relationship, end),
        });

        var info = Assert.Single(await new GraphResultProcessor(factory).ProcessAsync(
            [record],
            typeof(DynamicRelationship),
            TestContext.Current.CancellationToken));
        var result = Assert.IsType<DynamicRelationship>(factory.Deserialize(info));

        Assert.Equal("start-public", result.StartNodeId);
        Assert.Equal("end-public", result.EndNodeId);
        Assert.Equal("KNOWS", result.Type);
    }

    [Fact]
    public async Task RelationshipWireFixture_ScalarMetadataRecoversExactClrType()
    {
        var start = Node("start-wire", "start-public", "Start", 1, 1m);
        var end = Node("end-wire", "end-public", "End", 2, 2m);
        var versionIndependentTypeName =
            $"{typeof(MetadataDerivedRelationship).FullName}, {typeof(MetadataDerivedRelationship).Assembly.GetName().Name}";
        var relationship = GraphValue.Relationship(
            "edge-wire",
            "SHARED_TYPE",
            start.ElementId!,
            end.ElementId!,
            new Dictionary<string, GraphValue>
            {
                [nameof(IEntity.Id)] = GraphValue.Scalar("relationship-public"),
                [nameof(IRelationship.Direction)] = GraphValue.Scalar(RelationshipDirection.Outgoing.ToString()),
                ["__metadata__"] = GraphValue.Scalar(versionIndependentTypeName),
            });
        var record = new GraphRecord(new Dictionary<string, GraphValue>
        {
            ["PathSegment"] = PathSegment(start, relationship, end),
        });

        var info = Assert.Single(await new GraphResultProcessor(factory).ProcessAsync(
            [record],
            typeof(MetadataBaseRelationship),
            TestContext.Current.CancellationToken));

        Assert.Equal(typeof(MetadataDerivedRelationship), info.ActualType);
        Assert.DoesNotContain("__metadata__", info.SimpleProperties.Keys);
    }

    [Fact]
    public async Task TypedSimpleCollections_PreserveValueTypesOrderAndNulls()
    {
        var firstId = Guid.Parse("7a2ef43f-dadf-4c88-a2f6-af730f87a963");
        var secondId = Guid.Parse("69bd4638-166e-428f-8fd2-3993338e865f");
        var node = GraphValue.Node(
            "collections-wire",
            [nameof(ValueCollectionNode)],
            new Dictionary<string, GraphValue>
            {
                [nameof(ValueCollectionNode.IntegerList)] = GraphValue.List(
                    [GraphValue.Scalar(1L), GraphValue.Scalar(2L), GraphValue.Scalar(3L)]),
                [nameof(ValueCollectionNode.IntegerArray)] = GraphValue.List(
                    [GraphValue.Scalar(4L), GraphValue.Scalar(5L)]),
                [nameof(ValueCollectionNode.Guids)] = GraphValue.List(
                    [GraphValue.Scalar(firstId.ToString()), GraphValue.Scalar(secondId.ToString())]),
                [nameof(ValueCollectionNode.Kinds)] = GraphValue.List(
                    [GraphValue.Scalar(nameof(CollectionKind.First)), GraphValue.Scalar(nameof(CollectionKind.Second))]),
                [nameof(ValueCollectionNode.NullableIntegers)] = GraphValue.List(
                    [GraphValue.Scalar(6L), GraphValue.Scalar(null), GraphValue.Scalar(7L)]),
            });

        var info = Assert.Single(await new GraphResultProcessor(factory).ProcessAsync(
            [NodeRecord(node)],
            typeof(ValueCollectionNode),
            TestContext.Current.CancellationToken));

        AssertSimpleCollection(info, nameof(ValueCollectionNode.IntegerList), ExpectedIntegerList);
        AssertSimpleCollection(info, nameof(ValueCollectionNode.IntegerArray), ExpectedIntegerArray);
        AssertSimpleCollection(info, nameof(ValueCollectionNode.Guids), new[] { firstId, secondId });
        AssertSimpleCollection(info, nameof(ValueCollectionNode.Kinds), new[] { CollectionKind.First, CollectionKind.Second });
        AssertSimpleCollection(info, nameof(ValueCollectionNode.NullableIntegers), new int?[] { 6, null, 7 });
    }

    [Fact]
    public async Task TypedSimpleCollection_WithNonEnumerableWireValue_ThrowsDiagnosticException()
    {
        var node = GraphValue.Node(
            "invalid-collections-wire",
            [nameof(ValueCollectionNode)],
            new Dictionary<string, GraphValue>
            {
                [nameof(ValueCollectionNode.IntegerList)] = GraphValue.Scalar(42),
            });

        var exception = await Assert.ThrowsAsync<GraphException>(() => new GraphResultProcessor(factory).ProcessAsync(
            [NodeRecord(node)],
            typeof(ValueCollectionNode),
            TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ValueCollectionNode.IntegerList), exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(List<int>).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DynamicMapWireProperty_PassesThroughAsDictionary()
    {
        var node = GraphValue.Node(
            "map-wire",
            ["Place"],
            new Dictionary<string, GraphValue>
            {
                [nameof(IEntity.Id)] = GraphValue.Scalar("place-1"),
                ["address"] = GraphValue.Map(new Dictionary<string, GraphValue>
                {
                    ["city"] = GraphValue.Scalar("Berlin"),
                    ["zip"] = GraphValue.Scalar("10115"),
                }),
            });

        var info = Assert.Single(await new GraphResultProcessor(factory).ProcessAsync(
            [NodeRecord(node)],
            typeof(DynamicNode),
            TestContext.Current.CancellationToken));
        var result = Assert.IsType<DynamicNode>(factory.Deserialize(info));

        var address = Assert.IsType<Dictionary<string, object>>(result.Properties["address"]);
        Assert.Equal("Berlin", address["city"]);
        Assert.Equal("10115", address["zip"]);
    }

    [Fact]
    public async Task NestedProjectionCollections_MaterializeNonEmptyAndEmptyArrays()
    {
        var records = new[]
        {
            ProjectionRecord(
                "Alice",
                [
                    GraphValue.Map(new Dictionary<string, GraphValue>
                    {
                        ["Name"] = GraphValue.Scalar("Bob"),
                        ["Age"] = GraphValue.Scalar(25),
                    }),
                ]),
            ProjectionRecord("Diana", []),
        };
        var materializer = new GraphResultMaterializer(factory, loggerFactory: null);

        var results = await materializer.MaterializeAsync<List<CollectionProjection>>(
            records,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        var friend = Assert.Single(results[0].Friends);
        Assert.Equal("Bob", friend.Name);
        Assert.Equal(25, friend.Age);
        Assert.Empty(results[1].Friends);
    }

    [Fact]
    public void WireFactories_CopyInputsAndRejectInvalidPaths()
    {
        var labels = new List<string> { "Person" };
        var properties = new Dictionary<string, GraphValue> { ["Id"] = GraphValue.Scalar("person-1") };
        var node = GraphValue.Node("wire-1", labels, properties);
        labels[0] = "Changed";
        properties["Id"] = GraphValue.Scalar("changed");

        Assert.Equal("Person", Assert.Single(node.Labels));
        Assert.Equal("person-1", node.Entries["Id"].ScalarValue);
        Assert.Throws<ArgumentException>(() => GraphValue.Path([node, node]));
    }

    private static void AssertSimpleCollection<T>(EntityInfo entity, string propertyName, IEnumerable<T> expected)
    {
        var collection = Assert.IsType<SimpleCollection>(entity.SimpleProperties[propertyName].Value);

        Assert.Equal(typeof(T), collection.ElementType);
        Assert.Equal(expected.Cast<object?>(), collection.Values.Select(value => value.Object));
        Assert.All(collection.Values, value => Assert.Equal(typeof(T), value.Type));
    }

    private static GraphRecord NodeRecord(GraphValue node, IReadOnlyList<GraphValue>? complexProperties = null) => new(
        new Dictionary<string, GraphValue>
        {
            ["Node"] = GraphValue.Map(new Dictionary<string, GraphValue>
            {
                ["Node"] = node,
                ["ComplexProperties"] = GraphValue.List(complexProperties ?? []),
            }),
        });

    private static GraphRecord ProjectionRecord(string name, IReadOnlyList<GraphValue> friends) => new(
        new Dictionary<string, GraphValue>
        {
            ["Name"] = GraphValue.Scalar(name),
            ["Friends"] = GraphValue.List(friends),
        });

    private static GraphValue Node(
        string wireId,
        string publicId,
        string name,
        long large,
        decimal precise) => GraphValue.Node(
            wireId,
            ["Person"],
            new Dictionary<string, GraphValue>
            {
                [nameof(IEntity.Id)] = GraphValue.Scalar(publicId),
                ["name"] = GraphValue.Scalar(name),
                ["large"] = GraphValue.Scalar(large),
                ["precise"] = GraphValue.Scalar(precise),
            });

    private static GraphValue Relationship(
        string wireId,
        string type,
        GraphValue start,
        GraphValue end,
        int sequence) => GraphValue.Relationship(
            wireId,
            type,
            start.ElementId!,
            end.ElementId!,
            new Dictionary<string, GraphValue>
            {
                [nameof(IEntity.Id)] = GraphValue.Scalar(wireId),
                [nameof(IRelationship.Direction)] = GraphValue.Scalar(RelationshipDirection.Outgoing.ToString()),
                ["SequenceNumber"] = GraphValue.Scalar(sequence),
            });

    private static GraphValue ComplexProperty(
        GraphValue parent,
        GraphValue relationship,
        GraphValue property,
        int sequence) => GraphValue.Map(new Dictionary<string, GraphValue>
        {
            ["ParentNode"] = parent,
            ["Relationship"] = relationship,
            ["SequenceNumber"] = GraphValue.Scalar(sequence),
            ["Property"] = property,
        });

    private static GraphValue PathSegment(GraphValue start, GraphValue relationship, GraphValue end) =>
        GraphValue.Map(new Dictionary<string, GraphValue>
        {
            ["StartNode"] = GraphValue.Map(new Dictionary<string, GraphValue>
            {
                ["Node"] = start,
                ["ComplexProperties"] = GraphValue.List([]),
            }),
            ["Relationship"] = relationship,
            ["EndNode"] = GraphValue.Map(new Dictionary<string, GraphValue>
            {
                ["Node"] = end,
                ["ComplexProperties"] = GraphValue.List([]),
            }),
        });

    private sealed record CollectionProjection(string Name, FriendProjection[] Friends);

    private sealed record FriendProjection(string Name, int Age);

    private record MetadataBaseRelationship(string StartNodeId, string EndNodeId)
        : Relationship(StartNodeId, EndNodeId);

    private sealed record MetadataDerivedRelationship(string StartNodeId, string EndNodeId)
        : MetadataBaseRelationship(StartNodeId, EndNodeId);

    private sealed record ValueCollectionNode : Node
    {
        public List<int> IntegerList { get; init; } = [];

        public int[] IntegerArray { get; init; } = [];

        public List<Guid> Guids { get; init; } = [];

        public CollectionKind[] Kinds { get; init; } = [];

        public int?[] NullableIntegers { get; init; } = [];
    }

    private enum CollectionKind
    {
        First,
        Second,
    }

    private sealed class ValueCollectionNodeSerializer : IEntitySerializer
    {
        public Type EntityType => typeof(ValueCollectionNode);

        public EntityInfo Serialize(object obj) => throw new NotSupportedException();

        public object Deserialize(EntityInfo entity) => throw new NotSupportedException();

        public EntitySchema GetSchema()
        {
            var properties = typeof(ValueCollectionNode)
                .GetProperties()
                .Where(property => property.Name is
                    nameof(ValueCollectionNode.IntegerList) or
                    nameof(ValueCollectionNode.IntegerArray) or
                    nameof(ValueCollectionNode.Guids) or
                    nameof(ValueCollectionNode.Kinds) or
                    nameof(ValueCollectionNode.NullableIntegers))
                .ToDictionary(
                    property => property.Name,
                    property => new PropertySchema(
                        property,
                        property.Name,
                        PropertyType.SimpleCollection,
                        property.PropertyType.IsArray
                            ? property.PropertyType.GetElementType()!
                            : property.PropertyType.GetGenericArguments()[0]));

            return new EntitySchema(
                typeof(ValueCollectionNode),
                nameof(ValueCollectionNode),
                IsNullable: false,
                IsSimple: false,
                properties,
                new Dictionary<string, PropertySchema>());
        }
    }
}

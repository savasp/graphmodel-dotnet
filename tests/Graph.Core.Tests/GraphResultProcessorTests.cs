// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Serialization;
using Cvoya.Graph.Serialization.Results;

namespace Cvoya.Graph.Core.Tests;

public class GraphResultProcessorTests
{
    private readonly EntityFactory factory = new();

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

    private static GraphRecord NodeRecord(GraphValue node, IReadOnlyList<GraphValue>? complexProperties = null) => new(
        new Dictionary<string, GraphValue>
        {
            ["Node"] = GraphValue.Map(new Dictionary<string, GraphValue>
            {
                ["Node"] = node,
                ["ComplexProperties"] = GraphValue.List(complexProperties ?? []),
            }),
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
}

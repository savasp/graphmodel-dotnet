// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.Serialization;

public sealed class InMemorySerializationIdentityTests
{
    [Fact]
    public void KeylessDynamicComplexValuePreservesIdentityLikePropertyNames()
    {
        var nested = new EntityInfo(
            typeof(DynamicNode),
            "NestedValue",
            [],
            new Dictionary<string, Property>
            {
                ["Id"] = Simple("Id", "user-id"),
                ["StartNodeId"] = Simple("StartNodeId", "user-start"),
                ["EndNodeId"] = Simple("EndNodeId", "user-end"),
                ["Direction"] = Simple("Direction", "user-direction"),
            },
            new Dictionary<string, Property>());
        var root = new EntityInfo(
            typeof(DynamicNode),
            "Root",
            ["Root"],
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>
            {
                ["Payload"] = new Property(null!, "Payload", false, nested, "PAYLOAD"),
            });

        var decomposed = EntityWriter.DecomposeNode(root);
        var state = StoreState.Empty.AddNode(
            decomposed.Node,
            decomposed.ComplexValueNodes,
            decomposed.ComplexEdges);
        var materialized = new EntityReader(new EntityFactory())
            .MaterializeNode<DynamicNode>(decomposed.Node, state);
        var payload = Assert.IsType<Dictionary<string, object?>>(materialized.Properties["PAYLOAD"]);

        Assert.Null(decomposed.Node.CompatibilityId);
        Assert.All(decomposed.ComplexValueNodes, node => Assert.Null(node.CompatibilityId));
        Assert.All(decomposed.ComplexEdges, edge => Assert.Null(edge.CompatibilityId));
        Assert.Equal("user-id", payload["Id"]);
        Assert.Equal("user-start", payload["StartNodeId"]);
        Assert.Equal("user-end", payload["EndNodeId"]);
        Assert.Equal("user-direction", payload["Direction"]);
    }

    [Fact]
    public void KeylessRelationshipSerializationDoesNotInjectIdentityOrEndpoints()
    {
        var entity = new EntityInfo(
            typeof(DynamicRelationship),
            "LINKS",
            [],
            new Dictionary<string, Property>(),
            new Dictionary<string, Property>());
        var record = EntityWriter.DecomposeRelationship(
            entity,
            Guid.NewGuid(),
            Guid.NewGuid(),
            RelationshipDirection.Outgoing);

        var roundTripped = EntityReader.BuildRelationshipInfo(
            record,
            typeof(DynamicRelationship),
            includeLegacyEndpointState: true);

        Assert.Null(record.CompatibilityId);
        Assert.DoesNotContain(nameof(IEntity.Id), roundTripped.SimpleProperties.Keys);
        Assert.DoesNotContain(nameof(IRelationship.StartNodeId), roundTripped.SimpleProperties.Keys);
        Assert.DoesNotContain(nameof(IRelationship.EndNodeId), roundTripped.SimpleProperties.Keys);
        Assert.DoesNotContain(nameof(Relationship.Direction), roundTripped.SimpleProperties.Keys);
    }

    private static Property Simple(string name, string value) =>
        new(null!, name, false, new SimpleValue(value, typeof(string)));
}

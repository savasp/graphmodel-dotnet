// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

public sealed class ReverseLabelCacheTests
{
    [Fact]
    public Task DynamicEntities_SharedName_NodeThenRelationshipWarm_MaterializeAsTypedEntities() =>
        AssertDynamicEntitiesMaterializeAsync<NodeFirstSharedNode, NodeFirstSharedRelationship>(
            "InMemory_SharedLabel_NodeFirst",
            relationshipFirst: false);

    [Fact]
    public Task DynamicEntities_SharedName_RelationshipThenNodeWarm_MaterializeAsTypedEntities() =>
        AssertDynamicEntitiesMaterializeAsync<RelationshipFirstSharedNode, RelationshipFirstSharedRelationship>(
            "InMemory_SharedLabel_RelationshipFirst",
            relationshipFirst: true);

    private static async Task AssertDynamicEntitiesMaterializeAsync<TNode, TRelationship>(
        string sharedName,
        bool relationshipFirst)
        where TNode : class, INode
        where TRelationship : class, IRelationship
    {
        await using var store = new InMemoryGraphStore();
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.Graph.SchemaRegistry.InitializeAsync(cancellationToken);

        if (relationshipFirst)
        {
            Labels.GetLabelFromType(typeof(TRelationship));
            Labels.GetLabelFromType(typeof(TNode));
        }
        else
        {
            Labels.GetLabelFromType(typeof(TNode));
            Labels.GetLabelFromType(typeof(TRelationship));
        }

        var node = new DynamicNode([sharedName], new Dictionary<string, object?>());
        var endpoint = new DynamicNode([$"{sharedName}_Endpoint"], new Dictionary<string, object?>());
        await store.Graph.CreateNodeAsync(node, cancellationToken: cancellationToken);
        await store.Graph.CreateNodeAsync(endpoint, cancellationToken: cancellationToken);

        var relationship = new DynamicRelationship(
            node.Id,
            endpoint.Id,
            sharedName,
            new Dictionary<string, object?>());
        await store.Graph.CreateRelationshipAsync(relationship, cancellationToken: cancellationToken);

        var typedNode = await store.Graph.GetNodeAsync<TNode>(node.Id, cancellationToken: cancellationToken);
        var typedRelationship = await store.Graph.GetRelationshipAsync<TRelationship>(
            relationship.Id,
            cancellationToken: cancellationToken);

        Assert.IsType<TNode>(typedNode);
        Assert.Equal(node.Id, typedNode.Id);
        Assert.IsType<TRelationship>(typedRelationship);
        Assert.Equal(relationship.Id, typedRelationship.Id);
        Assert.Equal(node.Id, typedRelationship.StartNodeId);
        Assert.Equal(endpoint.Id, typedRelationship.EndNodeId);
    }
}

[Node("InMemory_SharedLabel_NodeFirst")]
internal sealed record NodeFirstSharedNode : Node;

[Relationship("InMemory_SharedLabel_NodeFirst")]
internal sealed record NodeFirstSharedRelationship : Relationship
{
    public NodeFirstSharedRelationship() : base(string.Empty, string.Empty) { }
}

[Node("InMemory_SharedLabel_RelationshipFirst")]
internal sealed record RelationshipFirstSharedNode : Node;

[Relationship("InMemory_SharedLabel_RelationshipFirst")]
internal sealed record RelationshipFirstSharedRelationship : Relationship
{
    public RelationshipFirstSharedRelationship() : base(string.Empty, string.Empty) { }
}

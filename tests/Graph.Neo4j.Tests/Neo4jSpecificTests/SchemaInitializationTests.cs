// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

public sealed class SchemaInitializationTests(Neo4jHarness harness) :
    Neo4jTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task UpdateNodeAsync_NonExistentNode_UninitializedRegistry_ThrowsEntityNotFound()
    {
        Assert.False(Graph.SchemaRegistry.IsInitialized);
        var node = new Class1 { Id = Guid.NewGuid().ToString("N") };

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.UpdateNodeAsync(node, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteNodeAsync_LegacyNodeWithoutEntityKind_Deletes()
    {
        Assert.False(Graph.SchemaRegistry.IsInitialized);
        var nodeId = Guid.NewGuid().ToString("N");
        await CreateLegacyNodeAsync(nodeId);

        await Graph.DeleteNodeAsync(nodeId, false, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, await CountLegacyNodesAsync(nodeId));
    }

    private async Task CreateLegacyNodeAsync(string nodeId)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "CREATE (:Class1 {Id: $nodeId, Property1: 'legacy'})";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new { nodeId });
        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<int> CountLegacyNodesAsync(string nodeId)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "MATCH (n:Class1 {Id: $nodeId}) RETURN COUNT(n) AS count";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new { nodeId });
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);

        return record["count"].As<int>();
    }
}

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

    [Fact]
    public async Task RecreateIndexesAsync_DropsAndRecreatesConfiguredIndexes()
    {
        await DropManagedIndexesAsync();
        await Graph.CreateNodeAsync(new Class1(), null, TestContext.Current.CancellationToken);

        const string staleIndexName = "idx_recreate_indexes_stale";
        var configuredIndexes = await GetManagedIndexNamesAsync();
        Assert.Contains("idx_configtestperson_firstname", configuredIndexes);
        Assert.Contains("idx_configtestknows_since", configuredIndexes);
        Assert.Contains("node_fulltext_index", configuredIndexes);
        Assert.Contains("rel_fulltext_index", configuredIndexes);

        await ExecuteSchemaCommandAsync(
            $"CREATE INDEX {staleIndexName} FOR (n:RecreateIndexesSentinel) ON (n.Value)");
        Assert.Contains(staleIndexName, await GetManagedIndexNamesAsync());

        await Graph.RecreateIndexesAsync(TestContext.Current.CancellationToken);

        var recreatedIndexes = await GetManagedIndexNamesAsync();
        Assert.DoesNotContain(staleIndexName, recreatedIndexes);
        Assert.Equal(configuredIndexes.Length, recreatedIndexes.Length);
        Assert.Equal(configuredIndexes, recreatedIndexes);
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

    private async Task ExecuteSchemaCommandAsync(string cypher)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        var result = await neo4jTransaction.Transaction.RunAsync(cypher);
        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<string[]> GetManagedIndexNamesAsync()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = """
            SHOW INDEXES YIELD name, type, owningConstraint
            WHERE (type = 'RANGE' OR type = 'FULLTEXT') AND owningConstraint IS NULL
            RETURN name
            ORDER BY name
            """;
        var result = await neo4jTransaction.Transaction.RunAsync(cypher);
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);

        return records.Select(record => record["name"].As<string>()).ToArray();
    }

    private async Task DropManagedIndexesAsync()
    {
        foreach (var indexName in await GetManagedIndexNamesAsync())
        {
            var escapedIndexName = indexName.Replace("`", "``", StringComparison.Ordinal);
            await ExecuteSchemaCommandAsync($"DROP INDEX `{escapedIndexName}` IF EXISTS");
        }
    }
}

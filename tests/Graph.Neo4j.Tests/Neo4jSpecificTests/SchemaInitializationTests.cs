// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Neo4j.Core;
using Cvoya.Graph.Neo4j.Querying.Cypher;
using global::Neo4j.Driver;

public sealed class SchemaInitializationTests : Neo4jTest
{
    private readonly Neo4jHarness harness;

    public SchemaInitializationTests(Neo4jHarness harness)
        : base(harness, StoreIsolation.FreshStore)
    {
        this.harness = harness;
    }

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
        Assert.Equal(configuredIndexes, recreatedIndexes);
    }

    [Fact]
    public async Task IndependentStores_ConcurrentInitialization_InstallsEquivalentSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();

        using var mutationBarrier = new Barrier(2);
        await using var firstDriver = new SchemaCommandBarrierDriver(
            Neo4jHarness.CreateIndependentDriver(),
            mutationBarrier,
            static cypher => cypher.StartsWith("CREATE CONSTRAINT ", StringComparison.Ordinal));
        await using var secondDriver = new SchemaCommandBarrierDriver(
            Neo4jHarness.CreateIndependentDriver(),
            mutationBarrier,
            static cypher => cypher.StartsWith("CREATE CONSTRAINT ", StringComparison.Ordinal));
        await using var firstStore = new Neo4jGraphStore(firstDriver, harness.CurrentDatabaseName);
        await using var secondStore = new Neo4jGraphStore(secondDriver, harness.CurrentDatabaseName);

        await Task.WhenAll(
            firstStore.Graph.CreateNodeAsync(
                new Class1 { Id = Guid.NewGuid().ToString("N"), Property1 = "first" },
                null,
                cancellationToken),
            secondStore.Graph.CreateNodeAsync(
                new Class1 { Id = Guid.NewGuid().ToString("N"), Property1 = "second" },
                null,
                cancellationToken));

        var constraints = await GetManagedConstraintNamesAsync();
        var indexes = await GetManagedIndexNamesAsync();
        Assert.Contains("unique_configtestperson_id", constraints);
        Assert.Contains("idx_configtestperson_firstname", indexes);
        Assert.Contains("node_fulltext_index", indexes);
        Assert.Contains("rel_fulltext_index", indexes);
    }

    [Fact]
    public async Task IndependentStores_ConcurrentRecreateIndexes_LeavesRequestedSchemaInstalled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();
        await Graph.CreateNodeAsync(new Class1(), null, cancellationToken);

        using var mutationBarrier = new Barrier(2);
        await using var firstDriver = new SchemaCommandBarrierDriver(
            Neo4jHarness.CreateIndependentDriver(),
            mutationBarrier,
            static cypher => cypher.StartsWith("DROP INDEX ", StringComparison.Ordinal));
        await using var secondDriver = new SchemaCommandBarrierDriver(
            Neo4jHarness.CreateIndependentDriver(),
            mutationBarrier,
            static cypher => cypher.StartsWith("DROP INDEX ", StringComparison.Ordinal));
        await using var firstStore = new Neo4jGraphStore(firstDriver, harness.CurrentDatabaseName);
        await using var secondStore = new Neo4jGraphStore(secondDriver, harness.CurrentDatabaseName);
        await Task.WhenAll(
            firstStore.Graph.SchemaRegistry.InitializeAsync(cancellationToken),
            secondStore.Graph.SchemaRegistry.InitializeAsync(cancellationToken));

        await Task.WhenAll(
            firstStore.Graph.RecreateIndexesAsync(cancellationToken),
            secondStore.Graph.RecreateIndexesAsync(cancellationToken));

        var indexes = await GetManagedIndexNamesAsync();
        Assert.Contains("idx_configtestperson_firstname", indexes);
        Assert.Contains("idx_configtestknows_since", indexes);
        Assert.Contains("node_fulltext_index", indexes);
        Assert.Contains("rel_fulltext_index", indexes);
    }

    [Fact]
    public async Task Initialization_IncompatibleNamedIndex_PreservesOriginalNeo4jError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();
        try
        {
            await ExecuteSchemaCommandAsync(
                "CREATE INDEX idx_configtestperson_firstname FOR (n:WrongLabel) ON (n.WrongProperty)");

            await using var driver = Neo4jHarness.CreateIndependentDriver();
            await using var store = new Neo4jGraphStore(driver, harness.CurrentDatabaseName);

            var exception = await Assert.ThrowsAsync<GraphException>(
                () => store.Graph.CreateNodeAsync(new Class1(), null, cancellationToken));
            var neo4jException = FindNeo4jException(exception);

            Assert.NotNull(neo4jException);
            Assert.Equal("Neo.ClientError.Schema.IndexWithNameAlreadyExists", neo4jException.Code);
            Assert.False(string.IsNullOrWhiteSpace(neo4jException.Message));
        }
        finally
        {
            // Database-pool cleanup removes data but intentionally retains schema. Remove the
            // deliberately incompatible index so a reused database cannot pollute another test.
            await DropManagedSchemaAsync();
        }
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

    private async Task<string[]> GetManagedConstraintNamesAsync()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "SHOW CONSTRAINTS YIELD name RETURN name ORDER BY name";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher);
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);

        return records.Select(record => record["name"].As<string>()).ToArray();
    }

    private async Task DropManagedIndexesAsync()
    {
        foreach (var indexName in await GetManagedIndexNamesAsync())
        {
            var escapedIndexName = CypherIdentifier.Escape(indexName, "index name");
            await ExecuteSchemaCommandAsync($"DROP INDEX {escapedIndexName} IF EXISTS");
        }
    }

    private async Task DropManagedSchemaAsync()
    {
        await DropManagedIndexesAsync();

        foreach (var constraintName in await GetManagedConstraintNamesAsync())
        {
            var escapedConstraintName = CypherIdentifier.Escape(constraintName, "constraint name");
            await ExecuteSchemaCommandAsync($"DROP CONSTRAINT {escapedConstraintName} IF EXISTS");
        }
    }

    private static Neo4jException? FindNeo4jException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is Neo4jException neo4jException)
            {
                return neo4jException;
            }
        }

        return null;
    }
}

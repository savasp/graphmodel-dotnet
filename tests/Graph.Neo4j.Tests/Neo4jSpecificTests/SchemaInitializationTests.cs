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
    public async Task UpdateAsync_EmptySelection_UninitializedRegistry_ReturnsZero()
    {
        Assert.False(Graph.SchemaRegistry.IsInitialized);

        var affected = await Graph.Nodes<Class1>()
            .Where(node => node.Property1 == "missing")
            .UpdateAsync(
                setters => setters.SetProperty(node => node.Property1, "updated"),
                TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task DeleteAsync_RawNodeWithoutFrameworkMetadata_Deletes()
    {
        Assert.False(Graph.SchemaRegistry.IsInitialized);
        var testKey = Guid.NewGuid().ToString("N");
        await CreateRawNodeAsync(testKey);

        var affected = await Graph.DynamicNodes().OfLabel("RawNativeNode")
            .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal(0, await CountRawNodesAsync(testKey));
    }

    [Fact]
    public async Task RecreateManagedIndexesAsync_RebuildsOwnedIndexesAndPreservesEverythingElse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedIndexesAsync();
        var indexedNode = new Class1 { Property1 = "managed rebuild token" };
        await Graph.CreateNodeAsync(indexedNode, null, cancellationToken);

        const string staleIndexName = "idx_retiredmodel_value";
        const string externalRangeIndexName = "external_range_index";
        const string externalFullTextIndexName = "external_fulltext_index";
        const string externalConstraintName = "external_unique_constraint";
        var configuredIndexes = await GetManagedIndexNamesAsync();
        Assert.Contains("idx_configtestperson_firstname", configuredIndexes);
        Assert.Contains("idx_configtestknows_since", configuredIndexes);
        Assert.Contains("node_fulltext_index", configuredIndexes);
        Assert.Contains("rel_fulltext_index", configuredIndexes);

        // A fixed-name provider-owned full-text index stays owned when its model-derived
        // definition becomes stale. RecreateManagedIndexesAsync must replace it.
        await ExecuteSchemaCommandAsync("DROP INDEX node_fulltext_index");
        await ExecuteSchemaCommandAsync(
            "CREATE FULLTEXT INDEX node_fulltext_index FOR (n:StaleLabel) ON EACH [n.StaleProperty]");

        // A stale deterministic-looking range name is not proof of ownership once its defining
        // model metadata is gone. Explicitly external RANGE/FULLTEXT indexes are equally outside
        // the operation, and constraints are never managed by it.
        await ExecuteSchemaCommandAsync(
            $"CREATE INDEX {staleIndexName} FOR (n:RecreateManagedIndexesSentinel) ON (n.Value)");
        await ExecuteSchemaCommandAsync(
            $"CREATE INDEX {externalRangeIndexName} FOR (n:ExternalRange) ON (n.Value)");
        await ExecuteSchemaCommandAsync(
            $"CREATE FULLTEXT INDEX {externalFullTextIndexName} FOR (n:ExternalFullText) ON EACH [n.Value]");
        await ExecuteSchemaCommandAsync(
            $"CREATE CONSTRAINT {externalConstraintName} FOR (n:ExternalUnique) REQUIRE n.Value IS UNIQUE");

        var beforeIndexIds = await GetIndexIdsAsync();
        Assert.All(
            configuredIndexes,
            indexName => Assert.True(beforeIndexIds.ContainsKey(indexName), $"Missing configured index {indexName}"));

        await Graph.RecreateManagedIndexesAsync(cancellationToken);

        var recreatedIndexes = await GetManagedIndexNamesAsync();
        var afterIndexIds = await GetIndexIdsAsync();
        var indexStates = await GetIndexStatesAsync();
        Assert.All(configuredIndexes, indexName => Assert.Equal("ONLINE", indexStates[indexName]));
        Assert.Equal(beforeIndexIds[staleIndexName], afterIndexIds[staleIndexName]);
        Assert.Equal(beforeIndexIds[externalRangeIndexName], afterIndexIds[externalRangeIndexName]);
        Assert.Equal(beforeIndexIds[externalFullTextIndexName], afterIndexIds[externalFullTextIndexName]);
        Assert.Contains(staleIndexName, recreatedIndexes);
        Assert.Contains(externalRangeIndexName, recreatedIndexes);
        Assert.Contains(externalFullTextIndexName, recreatedIndexes);
        Assert.Contains(externalConstraintName, await GetManagedConstraintNamesAsync());

        // Neo4j may reuse an internal schema ID after an index is dropped, so ID inequality is not
        // a stable replacement oracle. The stale reserved definition is the observable proof that
        // the provider replaced its owned index; the ID assertions above prove external artifacts
        // were preserved, and the state/search assertions prove the rebuilt indexes are usable.
        var fullTextLabels = await GetIndexLabelsOrTypesAsync("node_fulltext_index");
        Assert.DoesNotContain("StaleLabel", fullTextLabels);
        Assert.Contains("Class1", fullTextLabels);

        var searchResults = await Graph.SearchNodes<Class1>("managed rebuild token").ToListAsync(cancellationToken);
        Assert.Contains(searchResults, node => node.Property1 == indexedNode.Property1);
    }

    [Theory]
    [InlineData(
        "idx_configtestperson_firstname",
        "CREATE INDEX idx_configtestperson_firstname FOR (n:ExternalLabel) ON (n.ExternalProperty)")]
    [InlineData(
        "node_fulltext_index",
        "CREATE FULLTEXT INDEX node_fulltext_index FOR ()-[r:ExternalType]-() ON EACH [r.ExternalProperty]")]
    public async Task RecreateManagedIndexesAsync_SameNamedIncompatibleIndex_PreservesIndexAndFails(
        string indexName,
        string createCypher)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();

        try
        {
            await Graph.CreateNodeAsync(new Class1(), null, cancellationToken);
            var escapedIndexName = CypherIdentifier.Escape(indexName, "index name");
            await ExecuteSchemaCommandAsync($"DROP INDEX {escapedIndexName}");
            await ExecuteSchemaCommandAsync(createCypher);
            var externalIndexId = (await GetIndexIdsAsync())[indexName];

            await Assert.ThrowsAsync<GraphException>(
                () => Graph.RecreateManagedIndexesAsync(cancellationToken));

            Assert.Equal(externalIndexId, (await GetIndexIdsAsync())[indexName]);
        }
        finally
        {
            await DropManagedSchemaAsync();
        }
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
                new Class1 { Property1 = "first" },
                null,
                cancellationToken),
            secondStore.Graph.CreateNodeAsync(
                new Class1 { Property1 = "second" },
                null,
                cancellationToken));

        var constraints = await GetManagedConstraintNamesAsync();
        var indexes = await GetManagedIndexNamesAsync();
        Assert.Contains("unique_configtestperson_email", constraints);
        Assert.Contains("idx_configtestperson_firstname", indexes);
        Assert.Contains("node_fulltext_index", indexes);
        Assert.Contains("rel_fulltext_index", indexes);
    }

    [Fact]
    public async Task IndependentStores_ConcurrentRecreateManagedIndexes_LeavesRequestedSchemaInstalled()
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
            firstStore.Graph.RecreateManagedIndexesAsync(cancellationToken),
            secondStore.Graph.RecreateManagedIndexesAsync(cancellationToken));

        var indexes = await GetManagedIndexNamesAsync();
        Assert.Contains("idx_configtestperson_firstname", indexes);
        Assert.Contains("idx_configtestknows_since", indexes);
        Assert.Contains("node_fulltext_index", indexes);
        Assert.Contains("rel_fulltext_index", indexes);
    }

    [Fact]
    public async Task RecreateManagedIndexesAsync_FailurePreservesExternalIndexesAndAllowsRetry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();
        await Graph.CreateNodeAsync(new Class1(), null, cancellationToken);
        await ExecuteSchemaCommandAsync(
            "CREATE INDEX external_failure_sentinel FOR (n:ExternalFailureSentinel) ON (n.Value)");
        var externalIndexId = (await GetIndexIdsAsync())["external_failure_sentinel"];

        var failNextCreate = 1;
        using var unusedBarrier = new Barrier(1);
        await using var driver = new SchemaCommandBarrierDriver(
            Neo4jHarness.CreateIndependentDriver(),
            unusedBarrier,
            static _ => false,
            cypher => cypher.StartsWith("CREATE INDEX ", StringComparison.Ordinal)
                && Interlocked.Exchange(ref failNextCreate, 0) == 1
                    ? new InvalidOperationException("Injected managed-index creation failure.")
                    : null);
        await using var store = new Neo4jGraphStore(driver, harness.CurrentDatabaseName);

        await Assert.ThrowsAsync<GraphException>(
            () => store.Graph.RecreateManagedIndexesAsync(cancellationToken));
        Assert.Equal(externalIndexId, (await GetIndexIdsAsync())["external_failure_sentinel"]);

        await store.Graph.RecreateManagedIndexesAsync(cancellationToken);

        var indexes = await GetManagedIndexNamesAsync();
        Assert.Contains("idx_configtestperson_firstname", indexes);
        Assert.Contains("idx_configtestknows_since", indexes);
        Assert.Contains("node_fulltext_index", indexes);
        Assert.Contains("rel_fulltext_index", indexes);
        Assert.Equal(externalIndexId, (await GetIndexIdsAsync())["external_failure_sentinel"]);
    }

    [Fact]
    public async Task RecreateManagedIndexesAsync_PreCancelled_PreservesExternalIndex()
    {
        await DropManagedSchemaAsync();
        await Graph.CreateNodeAsync(new Class1(), null, TestContext.Current.CancellationToken);
        await ExecuteSchemaCommandAsync(
            "CREATE INDEX external_cancellation_sentinel FOR (n:ExternalCancellationSentinel) ON (n.Value)");
        var externalIndexId = (await GetIndexIdsAsync())["external_cancellation_sentinel"];
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.RecreateManagedIndexesAsync(cancellation.Token));

        Assert.Equal(externalIndexId, (await GetIndexIdsAsync())["external_cancellation_sentinel"]);
    }

    [Fact]
    public async Task Initialization_OutdatedGeneralFullTextIndex_RecreatesIndexForCurrentModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DropManagedSchemaAsync();

        // Simulate a database initialized by an older model version: the provider-owned
        // general full-text index exists but covers a different label/property set.
        await ExecuteSchemaCommandAsync(
            "CREATE FULLTEXT INDEX node_fulltext_index FOR (n:StaleLabel) ON EACH [n.StaleProperty]");

        await using var driver = Neo4jHarness.CreateIndependentDriver();
        await using var store = new Neo4jGraphStore(driver, harness.CurrentDatabaseName);

        await store.Graph.CreateNodeAsync(new Class1(), null, cancellationToken);

        var labels = await GetIndexLabelsOrTypesAsync("node_fulltext_index");
        Assert.DoesNotContain("StaleLabel", labels);
        Assert.Contains("Class1", labels);
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

    private async Task CreateRawNodeAsync(string testKey)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "CREATE (:RawNativeNode {testKey: $testKey, value: 'raw'})";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new { testKey });
        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<int> CountRawNodesAsync(string testKey)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "MATCH (n:RawNativeNode {testKey: $testKey}) RETURN COUNT(n) AS count";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new { testKey });
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

    private async Task<IReadOnlyDictionary<string, long>> GetIndexIdsAsync()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = """
            SHOW INDEXES YIELD id, name, type, owningConstraint
            WHERE (type = 'RANGE' OR type = 'FULLTEXT') AND owningConstraint IS NULL
            RETURN id, name
            """;
        var result = await neo4jTransaction.Transaction.RunAsync(cypher);
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);

        return records.ToDictionary(
            record => record["name"].As<string>(),
            record => record["id"].As<long>(),
            StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetIndexStatesAsync()
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = "SHOW INDEXES YIELD name, state RETURN name, state";
        var result = await neo4jTransaction.Transaction.RunAsync(cypher);
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);

        return records.ToDictionary(
            record => record["name"].As<string>(),
            record => record["state"].As<string>(),
            StringComparer.Ordinal);
    }

    private async Task<string[]> GetIndexLabelsOrTypesAsync(string indexName)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = """
            SHOW INDEXES YIELD name, labelsOrTypes
            WHERE name = $name
            RETURN labelsOrTypes
            """;
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new { name = indexName });
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);

        return [.. record["labelsOrTypes"].As<List<string>>()];
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

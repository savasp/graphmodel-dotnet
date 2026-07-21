// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying.Cypher.Execution;
using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
using Npgsql;
using Npgsql.Age;

public sealed class AgeSubgraphBatchTests(AgeGraphCleanupFixture graphCleanup)
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres";

    [Fact]
    public async Task CreateAsync_NestedComplexProperties_ExecutesOneBatch()
    {
        var batchExecutions = 0;
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_batch",
            TestContext.Current.CancellationToken,
            (source, graphName) => new AgeGraphStore(
                source,
                graphName,
                schemaRegistry: null,
                loggerFactory: null,
                () => Interlocked.Increment(ref batchExecutions)));

        // Warm lazy schema initialization and the measured graph path before resetting the seam.
        await CreateNestedSubgraphAsync(store.Graph);
        Interlocked.Exchange(ref batchExecutions, 0);

        var (source, target) = await CreateNestedSubgraphAsync(store.Graph);

        Assert.Equal(1, Volatile.Read(ref batchExecutions));
        var fetchedSource = await store.Graph.GetNodeAsync<Class1>(
            source.Id,
            null,
            TestContext.Current.CancellationToken);
        var fetchedTarget = await store.Graph.GetNodeAsync<Class1>(
            target.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.Equal("source nested", fetchedSource.A?.B?.Property1);
        Assert.Equal("target nested", fetchedTarget.A?.B?.Property1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_LaterBatchValidationFailure_RollsBackEveryWrite(bool callerOwnsTransaction)
    {
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_batch",
            TestContext.Current.CancellationToken);
        var graph = store.Graph;
        var cancellationToken = TestContext.Current.CancellationToken;

        var existingSource = new Person { FirstName = "Existing source" };
        var existingTarget = new Person { FirstName = "Existing target" };
        await graph.CreateNodeAsync(existingSource, null, cancellationToken);
        await graph.CreateNodeAsync(existingTarget, null, cancellationToken);
        var existingRelationship = new Knows(existingSource, existingTarget);
        await graph.CreateRelationshipAsync(existingRelationship, null, cancellationToken);

        var newSource = new Person { FirstName = "Rolled back source" };
        var newTarget = new Person { FirstName = "Rolled back target" };
        var duplicateRelationship = new Knows(newSource, newTarget)
        {
            Id = existingRelationship.Id,
        };

        if (callerOwnsTransaction)
        {
            await using var transaction = await graph.GetTransactionAsync(cancellationToken);
            await Assert.ThrowsAsync<GraphException>(() => graph.CreateAsync(
                newSource,
                duplicateRelationship,
                newTarget,
                null,
                transaction,
                cancellationToken));

            var survivor = new Person { FirstName = "After savepoint rollback" };
            await graph.CreateNodeAsync(survivor, transaction, cancellationToken);
            await transaction.CommitAsync();
            Assert.Equal(
                survivor.Id,
                (await graph.GetNodeAsync<Person>(survivor.Id, null, cancellationToken)).Id);
        }
        else
        {
            await Assert.ThrowsAsync<GraphException>(() => graph.CreateAsync(
                newSource,
                duplicateRelationship,
                newTarget,
                null,
                null,
                cancellationToken));
        }

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            graph.GetNodeAsync<Person>(newSource.Id, null, cancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            graph.GetNodeAsync<Person>(newTarget.Id, null, cancellationToken));
        Assert.Equal(
            existingRelationship.Id,
            (await graph.GetRelationshipAsync<Knows>(existingRelationship.Id, null, cancellationToken)).Id);
    }

    [Fact]
    public async Task CreateAsync_CreateOnly_SameEndpointIds_CreatesDistinctEndpointNodes()
    {
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_batch",
            TestContext.Current.CancellationToken);
        var node = new Person { FirstName = "Self" };
        var relationship = new Knows(node, node);

        await store.Graph.CreateAsync(
            node,
            relationship,
            node,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            await store.Graph.Nodes<Person>()
                .Where(person => person.Id == node.Id)
                .CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await store.Graph.Relationships<Knows>()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_CreateMissingEndpoints_SupportsSelfRelationship()
    {
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_batch",
            TestContext.Current.CancellationToken);
        var node = new Person { FirstName = "Self" };
        var relationship = new Knows(node, node);

        await store.Graph.CreateAsync(
            node,
            relationship,
            node,
            new GraphOperationOptions { CreateMissingEndpoints = true },
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            node.Id,
            (await store.Graph.GetNodeAsync<Person>(node.Id, null, TestContext.Current.CancellationToken)).Id);
        Assert.Equal(
            relationship.Id,
            (await store.Graph.GetRelationshipAsync<Knows>(
                relationship.Id,
                null,
                TestContext.Current.CancellationToken)).Id);
        Assert.Single(await store.Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EndpointIntentBatches_LeaveNoCorrelationOrSyntheticComplexIds()
    {
        var batchExecutions = 0;
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_transient",
            cancellationToken,
            (source, graphName) => new AgeGraphStore(
                source,
                graphName,
                schemaRegistry: null,
                loggerFactory: null,
                () => Interlocked.Increment(ref batchExecutions)));

        var selectedSource = new Person { FirstName = "Selected source" };
        var selectedTarget = new Person { FirstName = "Selected target" };
        await store.Graph.CreateNodeAsync(selectedSource, cancellationToken: cancellationToken);
        await store.Graph.CreateNodeAsync(selectedTarget, cancellationToken: cancellationToken);
        var selectedQuery = store.Graph.Nodes<Person>()
            .Where(person => person.Id == selectedSource.Id);
        var selectedTargetQuery = store.Graph.Nodes<Person>()
            .Where(person => person.Id == selectedTarget.Id);
        await CreateSelectedSelectedCommandAsync(
            selectedQuery,
            new Knows(string.Empty, string.Empty),
            selectedTargetQuery,
            cancellationToken);
        var hybridTarget = CreateNestedNode("hybrid");
        await CreateSelectedNewCommandAsync(
            selectedQuery,
            new Knows(string.Empty, string.Empty),
            hybridTarget,
            cancellationToken);

        var collectionEndpoint = new Class2
        {
            Property1 = "shared collection root",
            A =
            {
                new ComplexClassA
                {
                    Property1 = "first",
                    B = new ComplexClassB { Property1 = "first nested" },
                },
                new ComplexClassA
                {
                    Property1 = "second",
                    B = new ComplexClassB { Property1 = "second nested" },
                },
            },
        };
        await CreateAllNewCommandAsync(
            store.Graph,
            collectionEndpoint,
            new Knows(string.Empty, string.Empty),
            collectionEndpoint,
            GraphRelationshipCreationMode.Standard,
            cancellationToken);

        var selfLoop = CreateNestedNode("self loop");
        await CreateAllNewCommandAsync(
            store.Graph,
            selfLoop,
            new Knows(string.Empty, string.Empty),
            selfLoop,
            GraphRelationshipCreationMode.SelfLoop,
            cancellationToken);

        Assert.Equal(3, Volatile.Read(ref batchExecutions));
        Assert.Equal(
            2,
            await store.Graph.Nodes<Class2>()
                .Where(node => node.Id == collectionEndpoint.Id)
                .CountAsync(cancellationToken));
        Assert.Equal("hybrid nested", (await store.Graph.GetNodeAsync<Class1>(
            hybridTarget.Id,
            cancellationToken: cancellationToken)).A?.B?.Property1);
        await AssertNoTransientCorrelationOrSyntheticComplexIdsAsync(store.Graph, cancellationToken);
    }

    [Theory]
    [InlineData("source_root", false)]
    [InlineData("target_root", false)]
    [InlineData("complex_level_0", false)]
    [InlineData("complex_level_1", false)]
    [InlineData("relationship", false)]
    [InlineData("cleanup", false)]
    [InlineData("source_root", true)]
    [InlineData("target_root", true)]
    [InlineData("complex_level_0", true)]
    [InlineData("complex_level_1", true)]
    [InlineData("relationship", true)]
    [InlineData("cleanup", true)]
    public async Task EndpointIntentBatch_ValidationFailure_RollsBackEveryLevel(
        string failedCommand,
        bool callerOwnsTransaction)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_failure",
            cancellationToken,
            (source, graphName) => new AgeGraphStore(
                source,
                graphName,
                schemaRegistry: null,
                loggerFactory: null,
                batchExecutionObserver: null,
                batchCommandTransform: command => command.Name == failedCommand
                    ? command with
                    {
                        Cypher = $"UNWIND [] AS ignored RETURN count(ignored) AS {command.ProjectionColumns.Single()}",
                    }
                    : command));
        var source = CreateNestedNode($"failed {failedCommand} source");
        var target = CreateNestedNode($"failed {failedCommand} target");
        var relationship = new Knows(string.Empty, string.Empty);

        if (callerOwnsTransaction)
        {
            await using var transaction = await store.Graph.GetTransactionAsync(cancellationToken);
            await Assert.ThrowsAsync<GraphException>(() => CreateAllNewCommandAsync(
                store.Graph,
                source,
                relationship,
                target,
                GraphRelationshipCreationMode.Standard,
                cancellationToken,
                transaction));

            await store.Graph.CreateNodeAsync(
                new Person { FirstName = "caller transaction remains usable" },
                transaction,
                cancellationToken);
            await transaction.CommitAsync();
        }
        else
        {
            await Assert.ThrowsAsync<GraphException>(() => CreateAllNewCommandAsync(
                store.Graph,
                source,
                relationship,
                target,
                GraphRelationshipCreationMode.Standard,
                cancellationToken));
        }

        Assert.Empty(await store.Graph.Nodes<Class1>().ToListAsync(cancellationToken));
        Assert.Empty(await store.Graph.Relationships<Knows>().ToListAsync(cancellationToken));
        await AssertNoTransientCorrelationOrSyntheticComplexIdsAsync(store.Graph, cancellationToken);
    }

    [Fact]
    public async Task ConcurrentEndpointIntentBatches_DoNotCrossCorrelateEqualValues()
    {
        const int operationCount = 12;
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dataSource = CreateDataSource();
        await using var store = await graphCleanup.CreateStoreAsync(
            dataSource,
            "cvoya_subgraph_concurrent",
            cancellationToken);

        await Task.WhenAll(Enumerable.Range(0, operationCount).Select(_ =>
        {
            var equalEndpoint = CreateNestedNode("parallel equal");
            return CreateAllNewCommandAsync(
                store.Graph,
                equalEndpoint,
                new Knows(string.Empty, string.Empty),
                equalEndpoint,
                GraphRelationshipCreationMode.Standard,
                cancellationToken);
        }));

        Assert.Equal(operationCount * 2, await store.Graph.Nodes<Class1>().CountAsync(cancellationToken));
        Assert.Equal(operationCount, await store.Graph.Relationships<Knows>().CountAsync(cancellationToken));
        await AssertNoTransientCorrelationOrSyntheticComplexIdsAsync(store.Graph, cancellationToken);
    }

    private static async Task<(Class1 Source, Class1 Target)> CreateNestedSubgraphAsync(IGraph graph)
    {
        var source = new Class1
        {
            Property1 = "source",
            A = new ComplexClassA
            {
                Property1 = "source level one",
                B = new ComplexClassB { Property1 = "source nested" },
            },
        };
        var target = new Class1
        {
            Property1 = "target",
            A = new ComplexClassA
            {
                Property1 = "target level one",
                B = new ComplexClassB { Property1 = "target nested" },
            },
        };
        var relationship = new Knows(source, target);
        await graph.CreateAsync(
            source,
            relationship,
            target,
            null,
            null,
            TestContext.Current.CancellationToken);
        return (source, target);
    }

    private static Class1 CreateNestedNode(string value) => new()
    {
        Property1 = value,
        A = new ComplexClassA
        {
            Property1 = $"{value} level one",
            B = new ComplexClassB { Property1 = $"{value} nested" },
        },
    };

    private static async Task CreateSelectedNewCommandAsync<TSource>(
        IGraphQueryable<TSource> source,
        IRelationship relationship,
        INode target,
        CancellationToken cancellationToken)
        where TSource : class, INode
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(source.Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selected = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    new GraphElementSelectionModel(
                        GraphQueryModelBuilder.Build(source.Expression),
                        GraphElementSelectionMode.ExactOne),
                    source.Expression,
                    GraphEndpointRole.Source,
                    token);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selected),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    RelationshipDirection.Outgoing,
                    GraphRelationshipCreationMode.Standard,
                    token);
                return true;
            },
            cancellationToken);
    }

    private static async Task CreateSelectedSelectedCommandAsync<TSource, TTarget>(
        IGraphQueryable<TSource> source,
        IRelationship relationship,
        IGraphQueryable<TTarget> target,
        CancellationToken cancellationToken)
        where TSource : class, INode
        where TTarget : class, INode
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(source.Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selectedSource = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    new GraphElementSelectionModel(
                        GraphQueryModelBuilder.Build(source.Expression),
                        GraphElementSelectionMode.ExactOne),
                    source.Expression,
                    GraphEndpointRole.Source,
                    token);
                var selectedTarget = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    new GraphElementSelectionModel(
                        GraphQueryModelBuilder.Build(target.Expression),
                        GraphElementSelectionMode.ExactOne),
                    target.Expression,
                    GraphEndpointRole.Target,
                    token);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selectedSource),
                    relationship,
                    new SelectedGraphCommandEndpoint(selectedTarget),
                    RelationshipDirection.Outgoing,
                    GraphRelationshipCreationMode.Standard,
                    token);
                return true;
            },
            cancellationToken);
    }

    private static async Task CreateAllNewCommandAsync(
        IGraph graph,
        INode source,
        IRelationship relationship,
        INode target,
        GraphRelationshipCreationMode mode,
        CancellationToken cancellationToken,
        IGraphTransaction? transaction = null)
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(
            graph.Nodes<INode>(transaction).Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                await context.CreateRelationshipAsync(
                    new NewGraphCommandEndpoint(source),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    RelationshipDirection.Outgoing,
                    mode,
                    token);
                return true;
            },
            cancellationToken);
    }

    private static async Task AssertNoTransientCorrelationOrSyntheticComplexIdsAsync(
        IGraph graph,
        CancellationToken cancellationToken)
    {
        await using var transaction = await graph.GetTransactionAsync(cancellationToken);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        await using var correlationResult = await runner.RunAsync(
            """
            MATCH (node)
            UNWIND keys(node) AS propertyName
            WITH node, propertyName
            WHERE propertyName STARTS WITH $prefix
            RETURN count(DISTINCT node) AS correlatedCount
            """,
            new { prefix = "__graphModelSubgraphCorrelation" },
            cancellationToken);
        var correlationRecord = await correlationResult.SingleAsync(cancellationToken);
        Assert.Equal(0, correlationRecord["correlatedCount"].As<int>());

        await using var identityResult = await runner.RunAsync(
            """
            MATCH ()-[ownership]->(value)
            WHERE ownership.__graphModelComplexProperty = true
            RETURN count(value) AS valueCount,
                   count(CASE WHEN value.Id IS NOT NULL THEN value ELSE null END) AS valueIdCount,
                   count(CASE WHEN ownership.Id IS NOT NULL THEN ownership ELSE null END) AS relationshipIdCount
            """,
            new { },
            cancellationToken);
        var identityRecord = await identityResult.SingleAsync(cancellationToken);
        Assert.Equal(0, identityRecord["valueIdCount"].As<int>());
        Assert.Equal(0, identityRecord["relationshipIdCount"].As<int>());
        await transaction.CommitAsync();
    }

    private static NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(ConnectionString);
        builder.UseAge();
        return builder.Build();
    }
}

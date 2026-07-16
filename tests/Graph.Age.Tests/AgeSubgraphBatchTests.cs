// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.CompatibilityTests;
using Npgsql;
using Npgsql.Age;

public sealed class AgeSubgraphBatchTests
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_NestedComplexProperties_ExecutesOneBatch(bool createMissingEndpoints)
    {
        var batchExecutions = 0;
        await using var dataSource = CreateDataSource();
        await using var store = new AgeGraphStore(
            dataSource,
            NewGraphName(),
            schemaRegistry: null,
            loggerFactory: null,
            () => Interlocked.Increment(ref batchExecutions));
        await store.CreateGraphIfNotExistsAsync(TestContext.Current.CancellationToken);

        // Warm lazy schema initialization and the measured graph path before resetting the seam.
        await CreateNestedSubgraphAsync(store.Graph, createMissingEndpoints);
        Interlocked.Exchange(ref batchExecutions, 0);

        var (source, target) = await CreateNestedSubgraphAsync(store.Graph, createMissingEndpoints);

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
        await using var store = new AgeGraphStore(dataSource, NewGraphName());
        await store.CreateGraphIfNotExistsAsync(TestContext.Current.CancellationToken);
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
    public async Task CreateAsync_CreateOnly_SameEndpointIds_ThrowsAndCreatesNothing()
    {
        await using var dataSource = CreateDataSource();
        await using var store = new AgeGraphStore(dataSource, NewGraphName());
        await store.CreateGraphIfNotExistsAsync(TestContext.Current.CancellationToken);
        var node = new Person { FirstName = "Self" };
        var relationship = new Knows(node, node);

        var exception = await Assert.ThrowsAsync<GraphException>(() => store.Graph.CreateAsync(
            node,
            relationship,
            node,
            null,
            null,
            TestContext.Current.CancellationToken));

        Assert.Equal("Create-only subgraph endpoints must have distinct IDs.", exception.Message);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            store.Graph.GetNodeAsync<Person>(node.Id, null, TestContext.Current.CancellationToken));
        Assert.Empty(await store.Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_CreateMissingEndpoints_SupportsSelfRelationship()
    {
        await using var dataSource = CreateDataSource();
        await using var store = new AgeGraphStore(dataSource, NewGraphName());
        await store.CreateGraphIfNotExistsAsync(TestContext.Current.CancellationToken);
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

    private static async Task<(Class1 Source, Class1 Target)> CreateNestedSubgraphAsync(
        IGraph graph,
        bool createMissingEndpoints)
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
        var options = createMissingEndpoints
            ? new GraphOperationOptions { CreateMissingEndpoints = true }
            : null;

        await graph.CreateAsync(
            source,
            relationship,
            target,
            options,
            null,
            TestContext.Current.CancellationToken);
        return (source, target);
    }

    private static NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(ConnectionString);
        builder.UseAge();
        return builder.Build();
    }

    private static string NewGraphName() => $"cvoya_subgraph_batch_{Guid.NewGuid():N}";
}

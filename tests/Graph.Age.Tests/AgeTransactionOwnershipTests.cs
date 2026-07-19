// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.CompatibilityTests;
using Npgsql;
using Npgsql.Age;

/// <summary>
/// Pins that AGE transaction ownership is decided by graph identity, not by configuration. The
/// provider-agnostic contract lives in <see cref="ITransactionTests"/>; these cases cover the
/// adversarial shape that suite cannot express - two stores sharing one data source and one AGE
/// graph name (#366).
/// </summary>
public sealed class AgeTransactionOwnershipTests
{
    private static readonly string ConnectionString = Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres";

    [Fact]
    public async Task TransactionFromStoreWithIdenticalDataSourceAndGraphName_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var graphName = NewGraphName();

        // One data source, one AGE graph, two stores: every setting a settings-comparison check
        // could look at is identical, so only the graph's own identity can tell them apart.
        await using var dataSource = CreateDataSource();
        await using var first = new AgeGraphStore(dataSource, graphName);
        await using var second = new AgeGraphStore(dataSource, graphName);
        await first.CreateGraphIfNotExistsAsync(cancellationToken);

        var seeded = new Person { FirstName = "AgeOwnership", LastName = "Seeded" };
        await first.Graph.CreateNodeAsync(seeded, null, cancellationToken);

        await using var transaction = await first.Graph.GetTransactionAsync(cancellationToken);

        var intruder = new Person { FirstName = "AgeOwnership", LastName = "Intruder" };
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.CreateNodeAsync(intruder, transaction, cancellationToken));
        Assert.Contains("different AGE graph store", exception.Message, StringComparison.Ordinal);

        await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.GetNodeAsync<Person>(seeded.Id, transaction, cancellationToken));
        await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.Nodes<Person>(transaction).ToListAsync(cancellationToken));

        // Rejected before any work: nothing was written through the borrowed transaction, and the
        // transaction itself is still usable on the graph that created it.
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => first.Graph.GetNodeAsync<Person>(intruder.Id, transaction, cancellationToken));

        var committed = new Person { FirstName = "AgeOwnership", LastName = "Committed" };
        await first.Graph.CreateNodeAsync(committed, transaction, cancellationToken);
        await transaction.CommitAsync();

        var persisted = await first.Graph.GetNodeAsync<Person>(committed.Id, null, cancellationToken);
        Assert.Equal("Committed", persisted.LastName);
    }

    [Fact]
    public async Task TransactionFromStoreWithIdenticalSettings_RemainsUsableAfterRejection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var graphName = NewGraphName();

        await using var dataSource = CreateDataSource();
        await using var first = new AgeGraphStore(dataSource, graphName);
        await using var second = new AgeGraphStore(dataSource, graphName);
        await second.CreateGraphIfNotExistsAsync(cancellationToken);

        await using var transaction = await second.Graph.GetTransactionAsync(cancellationToken);

        // Rejecting a foreign transaction must not commit, roll back, or dispose it: the owner can
        // still roll it back explicitly, and the staged write must not survive.
        var staged = new Person { FirstName = "AgeOwnership", LastName = "RolledBack" };
        await second.Graph.CreateNodeAsync(staged, transaction, cancellationToken);

        await Assert.ThrowsAsync<GraphException>(
            () => first.Graph.DeleteNodeAsync(staged.Id, false, transaction, cancellationToken));

        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => second.Graph.GetNodeAsync<Person>(staged.Id, null, cancellationToken));
    }

    private static NpgsqlDataSource CreateDataSource()
    {
        var builder = new NpgsqlDataSourceBuilder(ConnectionString);
        builder.UseAge();
        return builder.Build();
    }

    private static string NewGraphName() => $"cvoya_tx_owner_{Guid.NewGuid():N}";
}

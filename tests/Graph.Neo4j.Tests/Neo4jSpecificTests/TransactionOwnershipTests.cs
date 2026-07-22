// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Pins that Neo4j transaction ownership is decided by graph identity, not by configuration. The
/// provider-agnostic contract lives in <see cref="ITransactionTests"/>; these cases cover the
/// adversarial shape that suite cannot express - two stores whose settings are not merely
/// equivalent but literally the same driver instance and the same database (#366).
/// </summary>
public sealed class TransactionOwnershipTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    private readonly Neo4jHarness harness = harness;

    [Fact]
    public async Task TransactionFromStoreWithIdenticalDriverAndDatabase_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databaseName = harness.CurrentDatabaseName;

        // One driver, one database, two stores: every setting a settings-comparison check could
        // look at is identical, so only the graph's own identity can tell these transactions apart.
        var driver = Neo4jHarness.CreateIndependentDriver();
        await using var driverLease = driver.ConfigureAwait(false);
        await using var first = new Neo4jGraphStore(driver, databaseName, null, Neo4jHarness.LoggerFactory);
        await using var second = new Neo4jGraphStore(driver, databaseName, null, Neo4jHarness.LoggerFactory);

        var seeded = new Person { FirstName = "Neo4jOwnership", LastName = "Seeded" };
        await first.Graph.CreateNodeAsync(seeded, null, cancellationToken);

        await using var transaction = await first.Graph.GetTransactionAsync(cancellationToken);

        var intruder = new Person { FirstName = "Neo4jOwnership", LastName = "Intruder" };
        var exception = await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.CreateNodeAsync(intruder, transaction, cancellationToken));
        Assert.Contains("different Neo4j graph store", exception.Message, StringComparison.Ordinal);

        await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.Nodes<Person>(transaction)
                .Where(person => person.TestKey == seeded.TestKey)
                .SingleAsync(cancellationToken));
        await Assert.ThrowsAsync<GraphException>(
            () => second.Graph.Nodes<Person>(transaction).ToListAsync(cancellationToken));

        // Rejected before any work: nothing was written through the borrowed transaction, and the
        // transaction itself is still usable on the graph that created it.
        Assert.Null(await first.Graph.Nodes<Person>(transaction)
            .Where(person => person.TestKey == intruder.TestKey)
            .SingleOrDefaultAsync(cancellationToken));

        var committed = new Person { FirstName = "Neo4jOwnership", LastName = "Committed" };
        await first.Graph.CreateNodeAsync(committed, transaction, cancellationToken);
        await transaction.CommitAsync();

        var persisted = await first.Graph.Nodes<Person>()
            .Where(person => person.TestKey == committed.TestKey)
            .SingleAsync(cancellationToken);
        Assert.Equal("Committed", persisted.LastName);
    }

    [Fact]
    public async Task TransactionFromStoreWithIdenticalSettings_RemainsUsableAfterRejection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var databaseName = harness.CurrentDatabaseName;

        var driver = Neo4jHarness.CreateIndependentDriver();
        await using var driverLease = driver.ConfigureAwait(false);
        await using var first = new Neo4jGraphStore(driver, databaseName, null, Neo4jHarness.LoggerFactory);
        await using var second = new Neo4jGraphStore(driver, databaseName, null, Neo4jHarness.LoggerFactory);

        await using var transaction = await second.Graph.GetTransactionAsync(cancellationToken);

        // Rejecting a foreign transaction must not commit, roll back, or dispose it: the owner can
        // still roll it back explicitly, and the staged write must not survive.
        var staged = new Person { FirstName = "Neo4jOwnership", LastName = "RolledBack" };
        await second.Graph.CreateNodeAsync(staged, transaction, cancellationToken);

        Assert.Throws<GraphException>(() => first.Graph.Nodes<Person>(transaction));

        await transaction.RollbackAsync();

        Assert.Null(await second.Graph.Nodes<Person>()
            .Where(person => person.TestKey == staged.TestKey)
            .SingleOrDefaultAsync(cancellationToken));
    }
}

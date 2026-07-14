// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// AGE-specific full-text search integration tests that go beyond the provider-neutral contract:
/// the phase-1 SQL runs on the caller's transaction, so it observes that transaction's uncommitted
/// writes.
/// </summary>
public sealed class AgeFullTextSearchIntegrationTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task Search_RunsOnCallerTransaction_SeesUncommittedWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var transaction = await this.Graph.GetTransactionAsync(ct);

        var person = new Person { FirstName = "Uncommitted", LastName = "TransientToken" };
        await this.Graph.CreateNodeAsync(person, transaction, ct);

        // Phase 1 executes on the same transaction as the write, so the uncommitted node is visible.
        var inTransaction = await this.Graph.SearchNodes<Person>("TransientToken", transaction)
            .ToListAsync(ct);
        Assert.Single(inTransaction);
        Assert.Equal(person.Id, inTransaction[0].Id);

        await transaction.RollbackAsync();

        // After rollback the write is gone, so a fresh search finds nothing.
        var afterRollback = await this.Graph.SearchNodes<Person>("TransientToken").ToListAsync(ct);
        Assert.Empty(afterRollback);
    }
}

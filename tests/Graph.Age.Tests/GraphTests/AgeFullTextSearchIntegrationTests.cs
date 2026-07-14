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

    [Fact]
    public async Task MixedSearch_AppliesOrderingPagingAndTerminalsAfterCombiningBothKinds()
    {
        var ct = TestContext.Current.CancellationToken;
        const string term = "MixedSearchBoundary";
        var first = new Person { FirstName = term, LastName = "Node" };
        var second = new Person { FirstName = "Endpoint", LastName = "Node" };
        await this.Graph.CreateNodeAsync(first, null, ct);
        await this.Graph.CreateNodeAsync(second, null, ct);

        var relationship = new KnowsWell
        {
            StartNodeId = first.Id,
            EndNodeId = second.Id,
            HowWell = $"{term} relationship",
        };
        await this.Graph.CreateRelationshipAsync(relationship, null, ct);

        var all = await this.Graph.Search(term).ToListAsync(ct);
        Assert.Equal(2, all.Count);

        var paged = await this.Graph.Search(term)
            .OrderBy(entity => entity.Id)
            .Take(1)
            .ToListAsync(ct);
        Assert.Single(paged);
        Assert.Equal(all.MinBy(entity => entity.Id)!.Id, paged[0].Id);

        Assert.Equal(2, await this.Graph.Search(term).CountAsync(ct));
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Schema;
using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// AGE-specific coverage for the retained explicit index-recreation API and the invariant that
/// current full-text search correctness does not depend on its managed artifacts.
/// </summary>
public sealed class AgeFullTextIndexTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task Search_StaysCorrect_WhenManagedIndexIsAbsentOrRecreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "expertise in cloud computing" };
        await this.Graph.CreateNodeAsync(alice, null, ct);

        var withoutManagedInfrastructure = await this.Graph.SearchNodes<Person>("cloud").ToListAsync(ct);
        Assert.Single(withoutManagedInfrastructure);

        // The drop is deliberately a no-op on a fresh current store: search must not require the
        // provider-reserved managed index to exist.
        string graphName;
        await using (var transaction = await this.Graph.GetTransactionAsync(ct))
        {
            var runner = ((AgeGraphTransaction)transaction).Runner;
            graphName = runner.GraphName;
            await runner.QueryScalarStringsAsync(
                $"DROP INDEX IF EXISTS {AgeSqlIdentifier.Quote(graphName, "graph name")}." +
                $"{AgeSqlIdentifier.Quote(AgeFullTextIndex.NodeIndexName, "index name")}",
                null,
                ct);
            await transaction.CommitAsync();
        }

        var withoutIndex = await this.Graph.SearchNodes<Person>("cloud").ToListAsync(ct);
        Assert.Single(withoutIndex);
        Assert.Equal(alice.Id, withoutIndex[0].Id);

        // Recreating the retained managed artifacts must not change current native search results.
        await this.Graph.RecreateIndexesAsync(ct);
        var recreated = await this.Graph.SearchNodes<Person>("cloud").ToListAsync(ct);
        Assert.Single(recreated);
        Assert.Equal(alice.Id, recreated[0].Id);
    }

    [Fact]
    public async Task RecreateIndexes_RebuildsAnExistingIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        await this.Graph.CreateNodeAsync(new Person { FirstName = "Index", LastName = "Identity" }, null, ct);
        await this.Graph.RecreateIndexesAsync(ct);
        var before = await ReadIndexOidAsync(AgeFullTextIndex.NodeIndexName, ct);
        await this.Graph.RecreateIndexesAsync(ct);
        var after = await ReadIndexOidAsync(AgeFullTextIndex.NodeIndexName, ct);

        Assert.NotEqual(before, after);
    }

    private async Task<string> ReadIndexOidAsync(string indexName, CancellationToken cancellationToken)
    {
        await using var transaction = await this.Graph.GetTransactionAsync(cancellationToken);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        var values = await runner.QueryScalarStringsAsync(
            "SELECT i.oid::text " +
            "FROM pg_class AS i " +
            "JOIN pg_namespace AS n ON n.oid = i.relnamespace " +
            $"WHERE n.nspname = @query AND i.relname = '{indexName}'",
            runner.GraphName,
            cancellationToken);
        await transaction.CommitAsync();
        return Assert.Single(values);
    }
}

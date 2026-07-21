// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.Age;
using Cvoya.Graph.Age.Core;
using Cvoya.Graph.Age.Querying;
using Cvoya.Graph.Age.Schema;
using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// AGE-specific tests for the full-text GIN index (#291): that the phase-1 SQL actually uses the index
/// once the planner has real statistics, and that dropping the index degrades performance but never
/// correctness.
/// </summary>
public sealed class AgeFullTextIndexTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task PhaseOneTypedSearch_UsesGinIndex_WhenTheStoreHasStatistics()
    {
        var ct = TestContext.Current.CancellationToken;
        string graphName;

        await this.Graph.RecreateIndexesAsync(ct);

        // Seed a few thousand rows so the planner prefers the index over a sequential scan; a handful
        // of rows would seq-scan regardless. Bulk-create via one Cypher UNWIND rather than per-node CRUD.
        await using (var seedTransaction = await this.Graph.GetTransactionAsync(ct))
        {
            var seedRunner = ((AgeGraphTransaction)seedTransaction).Runner;
            graphName = seedRunner.GraphName;
            await seedRunner.RunAsync(
                "UNWIND range(1, 3000) AS i " +
                "CREATE (n:CvoyaNode {Id: 'p' + toString(i), Bio: 'filler text number ' + toString(i), " +
                "inheritance_labels: ['Person']}) RETURN count(n)",
                null,
                ct);
            await seedRunner.RunAsync(
                "CREATE (n:CvoyaNode {Id: 'needle', Bio: 'uniqueneedletoken here', " +
                "inheritance_labels: ['Person']}) RETURN true",
                null,
                ct);
            await seedTransaction.CommitAsync();
        }

        await using var transaction = await this.Graph.GetTransactionAsync(ct);
        var runner = ((AgeGraphTransaction)transaction).Runner;
        await runner.QueryScalarStringsAsync(
            $"ANALYZE {AgeSqlIdentifier.Quote(graphName, "graph name")}.\"CvoyaNode\"", null, ct);

        var typedSql = AgeFullTextSearch.BuildTypedSql(
            graphName,
            "CvoyaNode",
            [new AgeFullTextSearch.FullTextCandidate("Person", ["Bio"])],
            hasManagedIndex: true);
        var plan = await runner.QueryScalarStringsAsync(
            "EXPLAIN (FORMAT JSON) " + typedSql, "uniqueneedletoken", ct);
        var planText = string.Join(string.Empty, plan);

        // Assert on the JSON plan, not prose: the coarse conjunct is served by a Bitmap Index Scan on
        // the new GIN index.
        Assert.Contains("Bitmap Index Scan", planText, StringComparison.Ordinal);
        Assert.Contains(AgeFullTextIndex.NodeIndexName, planText, StringComparison.Ordinal);

        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Search_StaysCorrect_WhenTheIndexIsDroppedAndRecreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice", LastName = "Wonder", Bio = "expertise in cloud computing" };
        await this.Graph.CreateNodeAsync(alice, null, ct);

        var withIndex = await this.Graph.SearchNodes<Person>("cloud").ToListAsync(ct);
        Assert.Single(withIndex);

        // Drop the index: the query must remain correct on the same code path (a sequential scan).
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

        // Recreating the index restores acceleration and keeps the result identical.
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

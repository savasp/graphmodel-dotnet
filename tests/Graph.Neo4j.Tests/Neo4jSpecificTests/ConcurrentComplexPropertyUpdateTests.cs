// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

/// <summary>
/// Pins the isolation behavior of concurrent complex-property updates (#219).
/// UpdateNodeAsync is delete-then-recreate for complex properties inside one transaction; two
/// concurrent updates of the same owner in separate transactions serialize on the owner node's
/// write lock (the simple-property SET runs before the complex-property delete/create), so the
/// final state is one writer's complete value subtree — never an interleaved mix or orphaned
/// value nodes. Under lock contention Neo4j may abort one transaction instead of serializing it;
/// the test tolerates that, requiring at least one writer to succeed.
/// </summary>
public sealed class ConcurrentComplexPropertyUpdateTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task ConcurrentUpdates_SeparateTransactions_OneValueNodeNoOrphans()
    {
        var original = new ComplexAddress { City = "Seattle", Street = "Concurrent Original" };
        var node = new ComplexPropertyOwner { Address = original };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var payloadA = new ComplexAddress { City = "Denver", Street = "Concurrent Writer A" };
        var payloadB = new ComplexAddress { City = "Austin", Street = "Concurrent Writer B" };

        var failures = await Task.WhenAll(
            UpdateInOwnTransactionAsync(node with { Address = payloadA }),
            UpdateInOwnTransactionAsync(node with { Address = payloadB }));

        // Neo4j may abort one transaction on lock contention, but never both.
        var succeeded = failures.Count(failure => failure is null);
        Assert.True(succeeded >= 1, $"Both concurrent updates failed: {failures[0]}; {failures[1]}");

        // Exactly one marker-bearing value node hangs off the owner...
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.Id }));

        // ...and no orphaned value nodes from any writer (or the original) survive anywhere.
        Assert.Equal(1, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.Street IN $streets RETURN count(a) AS count",
            new { streets = new[] { original.Street, payloadA.Street, payloadB.Street } }));

        // The surviving value is one successful writer's complete payload — not a mix.
        var fetched = await Graph.GetNodeAsync<ComplexPropertyOwner>(
            node.Id, null, TestContext.Current.CancellationToken);
        var winners = new List<ComplexAddress>();
        if (failures[0] is null) winners.Add(payloadA);
        if (failures[1] is null) winners.Add(payloadB);
        Assert.Contains(fetched.Address, winners);
    }

    [Fact]
    public async Task ConcurrentCollectionUpdates_SeparateTransactions_OneWritersItemsNoOrphans()
    {
        var original = new List<ComplexAddress>
        {
            new() { City = "Seattle", Street = "Collection Original 1" },
            new() { City = "Tacoma", Street = "Collection Original 2" }
        };
        var node = new ComplexPropertyCollectionOwner { Addresses = original };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var payloadA = new List<ComplexAddress>
        {
            new() { City = "Denver", Street = "Collection Writer A1" },
            new() { City = "Boulder", Street = "Collection Writer A2" },
            new() { City = "Golden", Street = "Collection Writer A3" }
        };
        var payloadB = new List<ComplexAddress>
        {
            new() { City = "Austin", Street = "Collection Writer B1" }
        };

        var failures = await Task.WhenAll(
            UpdateInOwnTransactionAsync(node with { Addresses = payloadA }),
            UpdateInOwnTransactionAsync(node with { Addresses = payloadB }));

        var succeeded = failures.Count(failure => failure is null);
        Assert.True(succeeded >= 1, $"Both concurrent updates failed: {failures[0]}; {failures[1]}");

        var fetched = await Graph.GetNodeAsync<ComplexPropertyCollectionOwner>(
            node.Id, null, TestContext.Current.CancellationToken);
        var winners = new List<List<ComplexAddress>>();
        if (failures[0] is null) winners.Add(payloadA);
        if (failures[1] is null) winners.Add(payloadB);
        Assert.Contains(winners, winner => winner.SequenceEqual(fetched.Addresses));

        // The value nodes in the store are exactly the winning collection — no partial leftovers.
        var allStreets = original.Concat(payloadA).Concat(payloadB).Select(a => a.Street).ToArray();
        Assert.Equal(fetched.Addresses.Count, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.Street IN $streets RETURN count(a) AS count",
            new { streets = allStreets }));
    }

    private async Task<Exception?> UpdateInOwnTransactionAsync<TNode>(TNode node)
        where TNode : class, Cvoya.Graph.INode
    {
        try
        {
            await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
            await Graph.UpdateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
            await transaction.CommitAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async Task<int> CountAsync(string cypher, object parameters)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();
        return record["count"].As<int>();
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Guards the multiplicity half of the self-loop traversal rule (#380). The deduplication added to
/// the in-memory neighbor expansion is deliberately scoped to a single physical relationship that
/// matches in both directions - it must not turn into a broad distinct-by-neighbor, which would
/// erase parallel edges.
/// <para>
/// These live here rather than in the compatibility suite because Neo4j's traversal projection
/// groups by target node, so it reports one result where a relationship-faithful provider reports
/// one per edge (#444). Promote them into <c>IQueryTraversalTests</c> once that is fixed. The
/// cross-provider self-loop cases that all providers do agree on are already pinned there.
/// </para>
/// </summary>
public sealed class SelfLoopTraversalTests : IAsyncLifetime
{
    private InMemoryGraphStore _store = null!;

    public ValueTask InitializeAsync()
    {
        _store = new InMemoryGraphStore();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _store.DisposeAsync();

    [Fact]
    public async Task ParallelSelfLoops_AreNotCollapsedByEndpointIdentity()
    {
        var alice = new Person { FirstName = "Alice" };
        await _store.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateRelationshipAsync(
            new Knows(alice, alice), null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateRelationshipAsync(
            new Knows(alice, alice), null, TestContext.Current.CancellationToken);

        var results = await _store.Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options.Direction(GraphTraversalDirection.Both))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, reached => Assert.Equal(alice.Id, reached.Id));
    }

    [Fact]
    public async Task OppositeDirectionRelationships_BetweenDistinctNodesRemainTwoResults()
    {
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        await _store.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateRelationshipAsync(
            new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateRelationshipAsync(
            new Knows(bob, alice), null, TestContext.Current.CancellationToken);

        var results = await _store.Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options.Direction(GraphTraversalDirection.Both))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, reached => Assert.Equal(bob.Id, reached.Id));
    }
}

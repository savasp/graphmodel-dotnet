// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Pins the self-loop degree semantics for the <see cref="GraphDegreeExtensions.CountRelationships"/>
/// projection surface (#300): a self-loop contributes 1 to Outgoing, 1 to Incoming, and 1 to Both,
/// matching Cypher's undirected <c>COUNT { (src)-[:R]-() }</c>. The
/// cross-provider contract test uses no self-loops, so this in-memory-only test guards the edge case.
/// </summary>
public sealed class DegreeProjectionSelfLoopTests : IAsyncLifetime
{
    private InMemoryGraphStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _store = new InMemoryGraphStore();
        var alice = new Person { FirstName = "Alice" };
        await _store.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await _store.Graph.CreateRelationshipAsync(
            new Knows(alice, alice), null, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _store.DisposeAsync();

    [Fact]
    public async Task SelfLoop_CountsOnceForEveryDirectionShape()
    {
        var stats = await _store.Graph.Nodes<Person>()
            .Select(p => new
            {
                Outgoing = p.CountRelationships<Knows>(GraphTraversalDirection.Outgoing),
                Incoming = p.CountRelationships<Knows>(GraphTraversalDirection.Incoming),
                Both = p.CountRelationships<Knows>(GraphTraversalDirection.Both),
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        var alice = Assert.Single(stats);
        Assert.Equal(1, alice.Outgoing);
        Assert.Equal(1, alice.Incoming);
        Assert.Equal(1, alice.Both);
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

public sealed class AgeRelationshipPredicateIntegrationTests(AgeHarness harness)
    : AgeTest(harness, StoreIsolation.FreshStore)
{
    [Fact]
    public async Task BothDirectionPredicatesStayAnchoredAndRejectAnyFailingHop()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var marker = $"AgeRelPredicate-{Guid.NewGuid():N}";
        var alice = new Person { FirstName = $"{marker}-alice" };
        var bob = new Person { FirstName = $"{marker}-bob" };
        var charlie = new Person { FirstName = $"{marker}-charlie" };
        var old = new Person { FirstName = $"{marker}-old" };
        foreach (var person in new[] { alice, bob, charlie, old })
        {
            await Graph.CreateNodeAsync(person, cancellationToken: cancellationToken);
        }

        await Graph.ConnectAsync(
            alice,
            new Knows { Since = cutoff.AddDays(1) },
            bob,
            cancellationToken: cancellationToken);
        await Graph.ConnectAsync(
            charlie,
            new Knows { Since = cutoff.AddDays(1) },
            bob,
            cancellationToken: cancellationToken);
        await Graph.ConnectAsync(
            old,
            new Knows { Since = cutoff.AddDays(-1) },
            alice,
            cancellationToken: cancellationToken);
        await Graph.ConnectAsync(
            alice,
            new Knows { Since = cutoff.AddDays(1) },
            alice,
            cancellationToken: cancellationToken);

        var traversed = await Graph.Nodes<Person>()
            .Where(person => person.TestKey == alice.TestKey)
            .Traverse<Knows, Person>(options => options
                .Direction(GraphTraversalDirection.Both)
                .Depth(1, 2)
                .WhereRelationship<Knows>(relationship => relationship.Since >= cutoff))
            .ToListAsync(cancellationToken);
        var existing = await Graph.Nodes<Person>()
            .Where(person => person.FirstName.StartsWith(marker))
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Both,
                relationship => relationship.Since >= cutoff)
            .ToListAsync(cancellationToken);

        Assert.Contains(traversed, person => person.TestKey == alice.TestKey);
        Assert.Contains(traversed, person => person.TestKey == bob.TestKey);
        Assert.Contains(traversed, person => person.TestKey == charlie.TestKey);
        Assert.DoesNotContain(traversed, person => person.TestKey == old.TestKey);
        Assert.Equal(
            new[] { alice.TestKey, bob.TestKey, charlie.TestKey }.Order(),
            existing.Select(person => person.TestKey).Order());
    }
}

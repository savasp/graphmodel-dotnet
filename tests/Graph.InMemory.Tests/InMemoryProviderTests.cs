// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using Cvoya.Graph.CompatibilityTests;

public sealed class InMemoryProviderTests
{
    [Fact]
    public async Task ToDictionaryAsync_PreservesRequestedKeyAndValueTypes()
    {
        await using var store = new InMemoryGraphStore();
        var group = $"dictionary-{Guid.NewGuid():N}";
        var alice = new Person { FirstName = "Alice", LastName = group };
        var bob = new Person { FirstName = "Bob", LastName = group };
        await store.Graph.CreateNodeAsync(alice, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(bob, cancellationToken: TestContext.Current.CancellationToken);

        var names = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == group)
            .ToDictionaryAsync(
                person => person.Id,
                person => person.FirstName,
                TestContext.Current.CancellationToken);

        Assert.Equal("Alice", names[alice.Id]);
        Assert.Equal("Bob", names[bob.Id]);

        var people = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == group)
            .ToDictionaryAsync(person => person.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Alice", people[alice.Id].FirstName);
        Assert.Equal("Bob", people[bob.Id].FirstName);
    }

    [Fact]
    public async Task ToLookupAsync_PreservesRequestedKeyAndValueTypes()
    {
        await using var store = new InMemoryGraphStore();
        var group = $"lookup-{Guid.NewGuid():N}";
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "Alice", LastName = group },
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "Bob", LastName = group },
            cancellationToken: TestContext.Current.CancellationToken);

        var names = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == group)
            .ToLookupAsync(
                person => person.LastName,
                person => person.FirstName,
                TestContext.Current.CancellationToken);

        Assert.Equal(["Alice", "Bob"], names[group].OrderBy(name => name));

        var people = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == group)
            .ToLookupAsync(person => person.LastName, TestContext.Current.CancellationToken);

        Assert.Equal(["Alice", "Bob"], people[group].Select(person => person.FirstName).OrderBy(name => name));
    }

    [Fact]
    public async Task TransactionFromAnotherStore_IsRejected()
    {
        await using var firstStore = new InMemoryGraphStore();
        await using var secondStore = new InMemoryGraphStore();
        await using var transaction = await firstStore.Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphException>(() =>
            secondStore.Graph.CreateNodeAsync(
                new Person { FirstName = "Wrong", LastName = "Store" },
                transaction,
                TestContext.Current.CancellationToken));

        Assert.Contains("not valid for this in-memory graph store", exception.Message);
    }

    [Fact]
    public async Task DistinctAfterTraversal_UsesStoredNodeIdentity()
    {
        await using var store = new InMemoryGraphStore();
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        await store.Graph.CreateNodeAsync(alice, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(bob, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            new Knows(alice.Id, bob.Id),
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            new Knows(alice.Id, bob.Id),
            cancellationToken: TestContext.Current.CancellationToken);

        var results = await store.Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>()
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(bob.Id, result.Id);
    }

    [Fact]
    public void Harness_DeclaresOnlyImplementedOptionalCapabilities()
    {
        var capabilities = new InMemoryHarness().Capabilities;

        Assert.True(capabilities.Has(GraphCapability.Transactions));
        Assert.True(capabilities.Has(GraphCapability.ComplexPropertyCascade));
        Assert.False(capabilities.Has(GraphCapability.FullTextSearch));
        Assert.False(capabilities.Has(GraphCapability.NestedTransactions));
        Assert.False(capabilities.Has(GraphCapability.CallSubqueries));
        Assert.False(capabilities.Has(GraphCapability.PatternSizeProjection));
        Assert.False(capabilities.Has(GraphCapability.MultiLabelMatch));
        Assert.False(capabilities.Has(GraphCapability.OrderByEntity));
        Assert.False(capabilities.Has(GraphCapability.ShortestPath));
        Assert.False(capabilities.Has(GraphCapability.OptionalTraversal));
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
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
    public async Task LeftChainedSetOperations_ExecuteEveryOperandInOrder()
    {
        await using var store = new InMemoryGraphStore();
        var marker = $"set-chain-{Guid.NewGuid():N}";
        var first = new Person { FirstName = "First", LastName = marker };
        var second = new Person { FirstName = "Second", LastName = marker };
        var third = new Person { FirstName = "Third", LastName = marker };
        await store.Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(third, cancellationToken: TestContext.Current.CancellationToken);

        var firstOnly = store.Graph.Nodes<Person>().Where(person => person.Id == first.Id);
        var secondOnly = store.Graph.Nodes<Person>().Where(person => person.Id == second.Id);
        var thirdOnly = store.Graph.Nodes<Person>().Where(person => person.Id == third.Id);

        var concatenated = await firstOnly
            .Concat(secondOnly)
            .Concat(thirdOnly)
            .ToListAsync(TestContext.Current.CancellationToken);
        var union = await firstOnly.Select(person => person.Id)
            .Union(secondOnly.Select(person => person.Id))
            .Union(firstOnly.Select(person => person.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([first.Id, second.Id, third.Id], concatenated.Select(person => person.Id));
        Assert.Equal(new[] { first.Id, second.Id }.Order(), union.Order());
    }

    [Fact]
    public async Task WholeEntityOrderingIsRejected()
    {
        await using var store = new InMemoryGraphStore();
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "unordered" },
            cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphQueryTranslationException>(() =>
            store.Graph.Nodes<Person>()
                .OrderBy(person => person)
                .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Whole-entity ordering", exception.Message);
    }

    [Fact]
    public async Task GenericRelationshipQueries_ExcludeComplexPropertyMarkerEdges()
    {
        await using var store = new InMemoryGraphStore();
        var owner = new PersonWithComplexProperty
        {
            FirstName = "owner",
            Address = new AddressValue { Street = "123 Main St", City = "Somewhere" },
        };
        var other = new Person { FirstName = "other" };
        await store.Graph.CreateNodeAsync(owner, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(other, cancellationToken: TestContext.Current.CancellationToken);
        var relationship = new Knows(owner, other) { Since = DateTime.UnixEpoch };
        await store.Graph.CreateRelationshipAsync(
            relationship,
            cancellationToken: TestContext.Current.CancellationToken);

        var relationships = await store.Graph.Relationships<IRelationship>()
            .ToListAsync(TestContext.Current.CancellationToken);
        var dynamicRelationships = await store.Graph.DynamicRelationships()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(relationships);
        Assert.Equal(relationship.Id, Assert.IsType<Knows>(relationships[0]).Id);
        Assert.Single(dynamicRelationships);
        Assert.Equal(relationship.Id, dynamicRelationships[0].Id);
    }

    [Fact]
    public async Task LegacyRelationshipUpdate_IgnoresDuplicateEndpointIdsElsewhere()
    {
        await using var store = new InMemoryGraphStore();
        var source = new Person { FirstName = "source" };
        var target = new Person { FirstName = "target" };
        await store.Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        var relationship = new Knows(source, target) { Since = DateTime.UnixEpoch };
        await store.Graph.CreateRelationshipAsync(
            relationship,
            cancellationToken: TestContext.Current.CancellationToken);

        // A later duplicate of the target's public ID must not affect a keyed property update
        // whose endpoints are unchanged.
        await store.Graph.CreateNodeAsync(
            new Person { Id = target.Id, FirstName = "duplicate-target" },
            cancellationToken: TestContext.Current.CancellationToken);
        var replacement = DateTime.UnixEpoch.AddDays(1);

        await store.Graph.UpdateRelationshipAsync(
            relationship with { Since = replacement },
            cancellationToken: TestContext.Current.CancellationToken);

        var stored = await store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(replacement, stored.Since);
    }

    [Fact]
    public async Task LegacyRelationshipUpdate_RejectsEndpointChange()
    {
        await using var store = new InMemoryGraphStore();
        var source = new Person { FirstName = "source" };
        var target = new Person { FirstName = "target" };
        var other = new Person { FirstName = "other" };
        foreach (var node in new[] { source, target, other })
        {
            await store.Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        }

        var relationship = new Knows(source, target) { Since = DateTime.UnixEpoch };
        await store.Graph.CreateRelationshipAsync(
            relationship,
            cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphException>(() => store.Graph.UpdateRelationshipAsync(
            relationship with { EndNodeId = other.Id, Since = DateTime.UnixEpoch.AddDays(1) },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("endpoints cannot be changed", exception.Message);
        var stored = await store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(DateTime.UnixEpoch, stored.Since);
    }

    [Fact]
    public void Harness_DeclaresOnlyImplementedOptionalCapabilities()
    {
        var capabilities = new InMemoryHarness().Capabilities;

        Assert.True(capabilities.Has(GraphCapability.Transactions));
        Assert.True(capabilities.Has(GraphCapability.ComplexPropertyCascade));
        // The interpreter executes correlated collection projections and pattern-size counts
        // natively by compiling the real projection lambda over grouped rows; see #120.
        Assert.True(capabilities.Has(GraphCapability.CallSubqueries));
        Assert.True(capabilities.Has(GraphCapability.PatternSizeProjection));
        Assert.True(capabilities.Has(GraphCapability.MultiLabelMatch));
        Assert.True(capabilities.Has(GraphCapability.LabelFiltering));
        Assert.True(capabilities.Has(GraphCapability.OptionalTraversal));
        // Naive whole-word matching over each entity's own searchable string properties; see #289.
        Assert.True(capabilities.Has(GraphCapability.FullTextSearch));
        // Scalar-key aggregation grouping runs natively over compiled grouping lambdas; see #306.
        Assert.True(capabilities.Has(GraphCapability.GroupByAggregation));
        Assert.False(capabilities.Has(GraphCapability.NestedTransactions));
        Assert.False(capabilities.Has(GraphCapability.OrderByEntity));
        Assert.True(capabilities.Has(GraphCapability.ShortestPath));
        Assert.True(capabilities.Has(GraphCapability.SetOperations));
    }
}

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
                person => person.TestKey,
                person => person.FirstName,
                TestContext.Current.CancellationToken);

        Assert.Equal("Alice", names[alice.TestKey]);
        Assert.Equal("Bob", names[bob.TestKey]);

        var people = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == group)
            .ToDictionaryAsync(person => person.TestKey, TestContext.Current.CancellationToken);

        Assert.Equal("Alice", people[alice.TestKey].FirstName);
        Assert.Equal("Bob", people[bob.TestKey].FirstName);
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
            store.Graph.Nodes<Person>().Where(person => person.TestKey == alice.TestKey),
            new Knows(),
            store.Graph.Nodes<Person>().Where(person => person.TestKey == bob.TestKey),
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            store.Graph.Nodes<Person>().Where(person => person.TestKey == alice.TestKey),
            new Knows(),
            store.Graph.Nodes<Person>().Where(person => person.TestKey == bob.TestKey),
            cancellationToken: TestContext.Current.CancellationToken);

        var results = await store.Graph.Nodes<Person>()
            .Where(person => person.TestKey == alice.TestKey)
            .Traverse<Knows, Person>()
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal(bob.TestKey, result.TestKey);
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

        var firstOnly = store.Graph.Nodes<Person>().Where(person => person.TestKey == first.TestKey);
        var secondOnly = store.Graph.Nodes<Person>().Where(person => person.TestKey == second.TestKey);
        var thirdOnly = store.Graph.Nodes<Person>().Where(person => person.TestKey == third.TestKey);

        var concatenated = await firstOnly
            .Concat(secondOnly)
            .Concat(thirdOnly)
            .ToListAsync(TestContext.Current.CancellationToken);
        var union = await firstOnly.Select(person => person.TestKey)
            .Union(secondOnly.Select(person => person.TestKey))
            .Union(firstOnly.Select(person => person.TestKey))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([first.TestKey, second.TestKey, third.TestKey], concatenated.Select(person => person.TestKey));
        Assert.Equal(new[] { first.TestKey, second.TestKey }.Order(), union.Order());
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
        var relationship = new Knows { Since = DateTime.UnixEpoch };
        await store.Graph.CreateRelationshipAsync(
            store.Graph.Nodes<PersonWithComplexProperty>().Where(person => person.TestKey == owner.TestKey),
            relationship,
            store.Graph.Nodes<Person>().Where(person => person.TestKey == other.TestKey),
            cancellationToken: TestContext.Current.CancellationToken);

        var relationships = await store.Graph.Relationships<IRelationship>()
            .ToListAsync(TestContext.Current.CancellationToken);
        var dynamicRelationships = await store.Graph.DynamicRelationships()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(relationships);
        Assert.Equal(relationship.TestKey, Assert.IsType<Knows>(relationships[0]).TestKey);
        Assert.Single(dynamicRelationships);
        Assert.Equal(Labels.GetLabelFromType(typeof(Knows)), dynamicRelationships[0].Type);
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

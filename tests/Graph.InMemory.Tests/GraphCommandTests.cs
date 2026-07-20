// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.CompatibilityTests;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

[Trait("Area", "GraphCommands")]
public sealed class GraphCommandTests
{
    [Fact]
    public async Task UpdateAsync_AppliesConstantAndComputedSettersToOrderedWindow()
    {
        await using var store = new InMemoryGraphStore();
        var marker = $"window-{Guid.NewGuid():N}";
        foreach (var age in new[] { 30, 10, 20, 40 })
        {
            await store.Graph.CreateNodeAsync(
                new Person { FirstName = $"person-{age}", LastName = marker, Age = age },
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var affected = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>()
                .Where(person => person.LastName == marker)
                .OrderBy(person => person.Age)
                .Skip(1)
                .Take(2),
            setters => setters
                .SetProperty(person => person.FirstName, "selected")
                .SetProperty(person => person.Age, person => person.Age + 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        var people = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == marker)
            .OrderBy(person => person.Age)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([10, 21, 31, 40], people.Select(person => person.Age));
        Assert.Equal(2, people.Count(person => person.FirstName == "selected"));
    }

    [Fact]
    public async Task UpdateAsync_UsesPrivateRecordIdentityInsteadOfDomainId()
    {
        await using var store = new InMemoryGraphStore();
        var sharedId = Guid.NewGuid().ToString("N");
        var person = new Person { Id = sharedId, FirstName = "person" };
        var manager = new Manager { Id = sharedId, FirstName = "manager", Department = "before" };
        await store.Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(manager, cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>().Where(candidate => candidate.Id == sharedId),
            setters => setters.SetProperty(candidate => candidate.FirstName, "updated"),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        var matches = await store.Graph.Nodes<Person>()
            .Where(candidate => candidate.Id == sharedId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal("updated", match.FirstName));
    }

    [Fact]
    public async Task UpdateAsync_SupportsDynamicBagKeysWithoutLeakingNativeIdentity()
    {
        await using var store = new InMemoryGraphStore();
        var originalNames = new[] { "old" };
        var node = new DynamicNode(
            ["Command Dynamic"],
            new Dictionary<string, object?> { ["score"] = 1, ["names"] = originalNames });
        await store.Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var replacement = new[] { "first", "second" };

        var affected = await GraphCommandExtensions.UpdateAsync(
            store.Graph.DynamicNodes().Where(candidate => candidate.Id == node.Id),
            setters => setters
                .SetProperty(candidate => candidate.Properties["score"], 2)
                .SetProperty(candidate => candidate.Properties["names"], replacement),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        var updated = await store.Graph.GetDynamicNodeAsync(
            node.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, updated.Properties["score"]);
        Assert.Equal(
            replacement,
            Assert.IsAssignableFrom<IEnumerable<object?>>(updated.Properties["names"]).Cast<string>());
        Assert.DoesNotContain("__nativeId", updated.Properties.Keys);
    }

    [Fact]
    public async Task RelationshipUpdateAndDelete_UseFrozenRelationshipTargets()
    {
        await using var store = new InMemoryGraphStore();
        var first = new Person { FirstName = "first" };
        var second = new Person { FirstName = "second" };
        var relationship = new Knows(first, second) { Since = DateTime.UnixEpoch };
        await store.Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(relationship, cancellationToken: TestContext.Current.CancellationToken);
        var replacement = DateTime.UnixEpoch.AddDays(1);

        var updated = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Relationships<Knows>().Where(candidate => candidate.Id == relationship.Id),
            setters => setters.SetProperty(candidate => candidate.Since, replacement),
            TestContext.Current.CancellationToken);
        var stored = await store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        var deleted = await GraphCommandExtensions.DeleteAsync(
            store.Graph.Relationships<Knows>().Where(candidate => candidate.Id == relationship.Id),
            cascadeDelete: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, updated);
        Assert.Equal(replacement, stored.Since);
        Assert.Equal(1, deleted);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_PreflightsWholeNodeSetAndRollsBackWhenCascadeIsDisabled()
    {
        await using var store = new InMemoryGraphStore();
        var marker = $"delete-{Guid.NewGuid():N}";
        var first = new Person { FirstName = "first", LastName = marker };
        var second = new Person { FirstName = "second", LastName = marker };
        var survivor = new Person { FirstName = "survivor" };
        var relationship = new Knows(first, survivor);
        await store.Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(survivor, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(relationship, cancellationToken: TestContext.Current.CancellationToken);
        var targets = store.Graph.Nodes<Person>().Where(person => person.LastName == marker);

        await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.DeleteAsync(
            targets,
            cascadeDelete: false,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, await targets.CountAsync(TestContext.Current.CancellationToken));
        _ = await store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.DeleteAsync(
            targets,
            cascadeDelete: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        Assert.Equal(0, await targets.CountAsync(TestContext.Current.CancellationToken));
        _ = await store.Graph.GetNodeAsync<Person>(
            survivor.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => store.Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MutationBoundToTransaction_IsVisibleOnlyInThatTransactionUntilCommit()
    {
        await using var store = new InMemoryGraphStore();
        var person = new Person { FirstName = "before" };
        await store.Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        await using var transaction = await store.Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>(transaction).Where(candidate => candidate.Id == person.Id),
            setters => setters.SetProperty(candidate => candidate.FirstName, "inside"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal("inside", (await store.Graph.GetNodeAsync<Person>(
            person.Id,
            transaction,
            TestContext.Current.CancellationToken)).FirstName);
        Assert.Equal("before", (await store.Graph.GetNodeAsync<Person>(
            person.Id,
            cancellationToken: TestContext.Current.CancellationToken)).FirstName);

        await transaction.CommitAsync();

        Assert.Equal("inside", (await store.Graph.GetNodeAsync<Person>(
            person.Id,
            cancellationToken: TestContext.Current.CancellationToken)).FirstName);
    }

    [Fact]
    public async Task UpdateAsync_CancellationLeavesStoreUnchanged()
    {
        await using var store = new InMemoryGraphStore();
        var person = new Person { FirstName = "before" };
        await store.Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>().Where(candidate => candidate.Id == person.Id),
            setters => setters.SetProperty(candidate => candidate.FirstName, "after"),
            cancellation.Token));

        Assert.Equal("before", (await store.Graph.GetNodeAsync<Person>(
            person.Id,
            cancellationToken: TestContext.Current.CancellationToken)).FirstName);
    }

    [Fact]
    public async Task ExactOneSelection_UsesDistinctTwoCandidateCardinalityProbe()
    {
        await using var store = new InMemoryGraphStore();
        var marker = $"cardinality-{Guid.NewGuid():N}";
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "first", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "second", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        var query = store.Graph.Nodes<Person>().Where(person => person.LastName == marker);
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(query.Provider);
        var selection = new GraphElementSelectionModel(
            GraphQueryModelBuilder.Build(query.Expression),
            GraphElementSelectionMode.ExactOne);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            provider.InWriteTransactionAsync(
                (context, token) => GraphCommandSelection.SelectExactOneAsync(
                    context,
                    selection,
                    query.Expression,
                    GraphEndpointRole.Source,
                    token),
                TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Source, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, exception.Failure);
    }

    [Fact]
    public async Task UpdateAsync_DynamicTypeChange_ReplacesStoredValueAndElementType()
    {
        await using var store = new InMemoryGraphStore();
        var originalNames = new[] { "old" };
        var node = new DynamicNode(
            ["CommandDynamicTypeChange"],
            new Dictionary<string, object?> { ["score"] = 1, ["names"] = originalNames });
        await store.Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var replacementNames = new[] { 5, 6 };

        var affected = await GraphCommandExtensions.UpdateAsync(
            store.Graph.DynamicNodes().Where(candidate => candidate.Id == node.Id),
            setters => setters
                .SetProperty(candidate => candidate.Properties["score"], "high")
                .SetProperty(candidate => candidate.Properties["names"], replacementNames),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        var updated = await store.Graph.GetDynamicNodeAsync(
            node.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("high", updated.Properties["score"]);
        Assert.Equal(replacementNames, Assert.IsType<List<int>>(updated.Properties["names"]));
    }

    [Fact]
    public async Task SelectNative_PredicateThatCannotBind_FailsInsteadOfWideningSelection()
    {
        await using var store = new InMemoryGraphStore();
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "only" },
            cancellationToken: TestContext.Current.CancellationToken);
        var query = store.Graph.Nodes<Person>();
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(query.Provider);
        Expression<Func<string, bool>> foreign = value => value.Length > 0;
        var selection = new GraphElementSelectionModel(
            new GraphQueryModel(
                new NodeRoot(typeof(Person)),
                predicates: [new PredicateFragment(foreign, alias: null)],
                traversal: [],
                projection: null,
                ordering: [],
                new Paging(null, null),
                TerminalOperation.ToListOrArray),
            GraphElementSelectionMode.Set);

        // The model validator rejects the unbindable predicate today; the executor's own
        // deferred-predicate guard backstops it. Either way the selection must fail instead of
        // silently widening the mutation target set.
        await Assert.ThrowsAnyAsync<GraphException>(() => provider.InWriteTransactionAsync(
            (context, token) => context.SelectAsync(selection, query.Expression, token),
            TestContext.Current.CancellationToken));
    }
}

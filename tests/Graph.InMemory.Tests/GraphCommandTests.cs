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

    [Fact]
    public async Task DuplicatePublicIdsRemainDistinctGraphElements()
    {
        await using var store = new InMemoryGraphStore();
        var sharedNodeId = Guid.NewGuid().ToString("N");
        var first = new Person { Id = sharedNodeId, FirstName = "same", LastName = "same" };
        var second = first with { };
        await store.Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);

        var nodeQuery = store.Graph.Nodes<Person>().Where(person => person.Id == sharedNodeId);
        Assert.Equal(2, await nodeQuery.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, (await nodeQuery.Distinct().ToListAsync(TestContext.Current.CancellationToken)).Count);

        var source = new Person { FirstName = "source" };
        var target = new Person { FirstName = "target" };
        await store.Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        var sharedRelationshipId = Guid.NewGuid().ToString("N");
        var relationship = new Knows(source, target)
        {
            Id = sharedRelationshipId,
            Since = DateTime.UnixEpoch,
        };
        await store.Graph.CreateRelationshipAsync(
            relationship,
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            relationship with { },
            cancellationToken: TestContext.Current.CancellationToken);

        var relationshipQuery = store.Graph.Relationships<Knows>()
            .Where(candidate => candidate.Id == sharedRelationshipId);
        Assert.Equal(2, await relationshipQuery.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            2,
            (await relationshipQuery.Distinct().ToListAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal(
            2,
            await GraphCommandExtensions.DeleteAsync(
                relationshipQuery,
                cascadeDelete: false,
                TestContext.Current.CancellationToken));
        Assert.Equal(0, await relationshipQuery.CountAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            2,
            await GraphCommandExtensions.DeleteAsync(
                nodeQuery,
                cascadeDelete: false,
                TestContext.Current.CancellationToken));
        Assert.Equal(0, await nodeQuery.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TraversalNeverCrossesValueEqualNodeRecords()
    {
        await using var store = new InMemoryGraphStore();
        var sourceId = Guid.NewGuid().ToString("N");
        var targetId = Guid.NewGuid().ToString("N");
        var sourceA = new Person { Id = sourceId, FirstName = "source-a" };
        var sourceB = new Person { Id = sourceId, FirstName = "source-b" };
        var targetA = new Person { Id = targetId, FirstName = "target-a" };
        var targetB = new Person { Id = targetId, FirstName = "target-b" };
        foreach (var node in new[] { sourceA, sourceB, targetA, targetB })
        {
            await store.Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        }

        await CreateSelectedRelationshipAsync(
            store.Graph.Nodes<Person>().Where(person => person.FirstName == sourceA.FirstName),
            new Knows(),
            store.Graph.Nodes<Person>().Where(person => person.FirstName == targetA.FirstName));
        await CreateSelectedRelationshipAsync(
            store.Graph.Nodes<Person>().Where(person => person.FirstName == sourceB.FirstName),
            new Knows(),
            store.Graph.Nodes<Person>().Where(person => person.FirstName == targetB.FirstName));

        _ = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>().Where(person => person.Id == sourceId),
            setters => setters.SetProperty(person => person.FirstName, "same-source"),
            TestContext.Current.CancellationToken);
        _ = await GraphCommandExtensions.UpdateAsync(
            store.Graph.Nodes<Person>().Where(person => person.Id == targetId),
            setters => setters.SetProperty(person => person.FirstName, "same-target"),
            TestContext.Current.CancellationToken);

        var reached = await store.Graph.Nodes<Person>()
            .Where(person => person.Id == sourceId)
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, reached.Count);
        Assert.All(reached, person => Assert.Equal(targetId, person.Id));
    }

    [Fact]
    public async Task EndpointIntentCreationSupportsSelectedNewAndSelfLoopForms()
    {
        await using var store = new InMemoryGraphStore();
        var selectedSource = new Person { FirstName = "selected-source" };
        var selectedTarget = new Person { FirstName = "selected-target" };
        await store.Graph.CreateNodeAsync(
            selectedSource,
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(
            selectedTarget,
            cancellationToken: TestContext.Current.CancellationToken);
        var sourceQuery = store.Graph.Nodes<Person>()
            .Where(person => person.FirstName == selectedSource.FirstName);
        var targetQuery = store.Graph.Nodes<Person>()
            .Where(person => person.FirstName == selectedTarget.FirstName);

        await CreateSelectedRelationshipAsync(sourceQuery, new Knows(), targetQuery);
        await CreateSelectedNewRelationshipAsync(
            sourceQuery,
            new Knows(),
            new Person { FirstName = "new-target" });
        await CreateNewSelectedRelationshipAsync(
            store.Graph,
            new Person { FirstName = "new-source" },
            new Knows(),
            targetQuery);

        var equalNode = new Person { FirstName = "equal-new" };
        await CreateNewRelationshipAsync(
            store.Graph,
            equalNode,
            new Knows(),
            equalNode,
            GraphRelationshipCreationMode.Standard);
        await CreateSelectedRelationshipAsync(sourceQuery, new Knows(), sourceQuery);

        var selfLoop = new Person { FirstName = "new-self-loop" };
        await CreateNewRelationshipAsync(
            store.Graph,
            selfLoop,
            new Knows(),
            selfLoop,
            GraphRelationshipCreationMode.SelfLoop);

        Assert.Equal(7, await store.Graph.Nodes<Person>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await store.Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            2,
            await store.Graph.Nodes<Person>()
                .Where(person => person.FirstName == equalNode.FirstName)
                .CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            1,
            await store.Graph.Nodes<Person>()
                .Where(person => person.FirstName == selfLoop.FirstName)
                .CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await store.Graph.Nodes<Person>()
            .Where(person => person.FirstName == selfLoop.FirstName)
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken));

        var cardinalityFailure = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            CreateSelectedRelationshipAsync(
                sourceQuery,
                new Knows(),
                store.Graph.Nodes<Person>().Where(person => person.FirstName == "missing-target")));
        Assert.Equal(GraphEndpointRole.Target, cardinalityFailure.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, cardinalityFailure.Failure);
        Assert.Equal(6, await store.Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));

        var ambiguousMarker = $"ambiguous-{Guid.NewGuid():N}";
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "first-ambiguous-target", LastName = ambiguousMarker },
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(
            new Person { FirstName = "second-ambiguous-target", LastName = ambiguousMarker },
            cancellationToken: TestContext.Current.CancellationToken);

        var multipleFailure = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            CreateSelectedRelationshipAsync(
                sourceQuery,
                new Knows(),
                store.Graph.Nodes<Person>().Where(person => person.LastName == ambiguousMarker)));
        Assert.Equal(GraphEndpointRole.Target, multipleFailure.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, multipleFailure.Failure);
        Assert.Equal(6, await store.Graph.Relationships<Knows>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TraversalDistinct_DedupesTargetReachedByTwoConvergingPaths()
    {
        await using var store = new InMemoryGraphStore();
        var source = new Person { FirstName = "source" };
        var target = new Person { FirstName = "target" };
        await store.Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            new Knows(source, target) { Since = DateTime.UnixEpoch },
            cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            new Knows(source, target) { Since = DateTime.UnixEpoch.AddDays(1) },
            cancellationToken: TestContext.Current.CancellationToken);
        var traversal = store.Graph.Nodes<Person>()
            .Where(person => person.Id == source.Id)
            .Traverse<Knows, Person>();

        // The parallel edges genuinely produce two rows before deduplication; Distinct then
        // collapses them because both rows bind one stored record key, not because the
        // materialized values are equal.
        Assert.Equal(2, await traversal.CountAsync(TestContext.Current.CancellationToken));
        var distinct = await traversal.Distinct().ToListAsync(TestContext.Current.CancellationToken);
        var single = Assert.Single(distinct);
        Assert.Equal(target.Id, single.Id);
    }

    [Fact]
    public async Task MutationsOverTraversalQueries_AreRejectedByTheSharedSelectionGrammar()
    {
        await using var store = new InMemoryGraphStore();
        var source = new Person { FirstName = "source" };
        var target = new Person { FirstName = "target" };
        await store.Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);
        await store.Graph.CreateRelationshipAsync(
            new Knows(source, target) { Since = DateTime.UnixEpoch },
            cancellationToken: TestContext.Current.CancellationToken);
        var traversal = store.Graph.Nodes<Person>()
            .Where(person => person.Id == source.Id)
            .Traverse<Knows, Person>();

        var updateFailure = await Assert.ThrowsAsync<GraphQueryTranslationException>(() =>
            GraphCommandExtensions.UpdateAsync(
                traversal,
                setters => setters.SetProperty(person => person.FirstName, "never"),
                TestContext.Current.CancellationToken));
        var deleteFailure = await Assert.ThrowsAsync<GraphQueryTranslationException>(() =>
            GraphCommandExtensions.DeleteAsync(
                traversal,
                cascadeDelete: true,
                TestContext.Current.CancellationToken));

        Assert.Contains("command selection", updateFailure.Message);
        Assert.Contains("command selection", deleteFailure.Message);
        Assert.Equal(
            "target",
            (await store.Graph.GetNodeAsync<Person>(
                target.Id,
                cancellationToken: TestContext.Current.CancellationToken)).FirstName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyOrderedWindow()
    {
        await using var store = new InMemoryGraphStore();
        var marker = $"delete-window-{Guid.NewGuid():N}";
        foreach (var age in new[] { 30, 10, 20, 40 })
        {
            await store.Graph.CreateNodeAsync(
                new Person { FirstName = $"person-{age}", LastName = marker, Age = age },
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var affected = await GraphCommandExtensions.DeleteAsync(
            store.Graph.Nodes<Person>()
                .Where(person => person.LastName == marker)
                .OrderBy(person => person.Age)
                .Skip(1)
                .Take(2),
            cascadeDelete: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        var survivors = await store.Graph.Nodes<Person>()
            .Where(person => person.LastName == marker)
            .OrderBy(person => person.Age)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([10, 40], survivors.Select(person => person.Age));
    }

    private static async Task CreateSelectedRelationshipAsync<TSource, TTarget>(
        IGraphQueryable<TSource> source,
        IRelationship relationship,
        IGraphQueryable<TTarget> target)
        where TSource : class, INode
        where TTarget : class, INode
    {
        var sourceProvider = Assert.IsAssignableFrom<IGraphCommandProvider>(source.Provider);
        var targetProvider = Assert.IsAssignableFrom<IGraphCommandProvider>(target.Provider);
        GraphCommandProviderScope.Validate(sourceProvider, targetProvider);
        await sourceProvider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selectedSource = await SelectEndpointAsync(
                    context,
                    source,
                    GraphEndpointRole.Source,
                    token);
                var selectedTarget = await SelectEndpointAsync(
                    context,
                    target,
                    GraphEndpointRole.Target,
                    token);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selectedSource),
                    relationship,
                    new SelectedGraphCommandEndpoint(selectedTarget),
                    RelationshipDirection.Outgoing,
                    GraphRelationshipCreationMode.Standard,
                    token);
                return true;
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task CreateSelectedNewRelationshipAsync<TSource>(
        IGraphQueryable<TSource> source,
        IRelationship relationship,
        INode target)
        where TSource : class, INode
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(source.Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selected = await SelectEndpointAsync(context, source, GraphEndpointRole.Source, token);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selected),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    RelationshipDirection.Outgoing,
                    GraphRelationshipCreationMode.Standard,
                    token);
                return true;
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task CreateNewSelectedRelationshipAsync<TTarget>(
        IGraph graph,
        INode source,
        IRelationship relationship,
        IGraphQueryable<TTarget> target)
        where TTarget : class, INode
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(target.Provider);
        var graphProvider = Assert.IsAssignableFrom<IGraphCommandProvider>(graph.Nodes<INode>().Provider);
        GraphCommandProviderScope.Validate(provider, graphProvider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selected = await SelectEndpointAsync(context, target, GraphEndpointRole.Target, token);
                await context.CreateRelationshipAsync(
                    new NewGraphCommandEndpoint(source),
                    relationship,
                    new SelectedGraphCommandEndpoint(selected),
                    RelationshipDirection.Outgoing,
                    GraphRelationshipCreationMode.Standard,
                    token);
                return true;
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task CreateNewRelationshipAsync(
        IGraph graph,
        INode source,
        IRelationship relationship,
        INode target,
        GraphRelationshipCreationMode mode)
    {
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(graph.Nodes<INode>().Provider);
        await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                await context.CreateRelationshipAsync(
                    new NewGraphCommandEndpoint(source),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    RelationshipDirection.Outgoing,
                    mode,
                    token);
                return true;
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<SelectedGraphElement> SelectEndpointAsync<TEntity>(
        IGraphCommandExecutionContext context,
        IGraphQueryable<TEntity> query,
        GraphEndpointRole role,
        CancellationToken cancellationToken)
        where TEntity : class, INode =>
        GraphCommandSelection.SelectExactOneAsync(
            context,
            new GraphElementSelectionModel(
                GraphQueryModelBuilder.Build(query.Expression),
                GraphElementSelectionMode.ExactOne),
            query.Expression,
            role,
            cancellationToken);
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>Provider contract tests for native target selection and set-based command execution.</summary>
public interface IGraphCommandTests : IGraphTest
{
    [Fact]
    public async Task KeyAndUniqueUpdate_CommitsCompleteFinalStateAndExactCount()
    {
        var marker = $"command-constraint-single-{Guid.NewGuid():N}";
        var node = new AtomicMutationNode
        {
            KeyGroup = marker,
            KeyCode = "old-key",
            Email = $"old-{marker}@example.com",
            Marker = marker,
        };
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicMutationNode>().Where(candidate => candidate.Marker == marker),
            setters => setters
                .SetProperty(candidate => candidate.KeyCode, "new-key")
                .SetProperty(candidate => candidate.Email, $"new-{marker}@example.com"),
            TestContext.Current.CancellationToken);

        var stored = await Graph.Nodes<AtomicMutationNode>()
            .SingleAsync(candidate => candidate.Marker == marker, TestContext.Current.CancellationToken);
        Assert.Equal(1, affected);
        Assert.Equal("new-key", stored.KeyCode);
        Assert.Equal($"new-{marker}@example.com", stored.Email);
    }

    [Fact]
    public async Task KeyAndUniqueUpdate_AllowsValidSelectedSetSwap()
    {
        var marker = $"command-constraint-swap-{Guid.NewGuid():N}";
        var firstKey = $"first-{Guid.NewGuid():N}";
        var secondKey = $"second-{Guid.NewGuid():N}";
        var firstEmail = $"first-{Guid.NewGuid():N}@example.com";
        var secondEmail = $"second-{Guid.NewGuid():N}@example.com";
        await Graph.CreateNodeAsync(new AtomicMutationNode
        {
            KeyGroup = marker,
            KeyCode = firstKey,
            Email = firstEmail,
            Marker = marker,
        }, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(new AtomicMutationNode
        {
            KeyGroup = marker,
            KeyCode = secondKey,
            Email = secondEmail,
            Marker = marker,
        }, cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicMutationNode>().Where(candidate => candidate.Marker == marker),
            setters => setters
                .SetProperty(
                    candidate => candidate.KeyCode,
                    candidate => candidate.KeyCode == firstKey ? secondKey : firstKey)
                .SetProperty(
                    candidate => candidate.Email,
                    candidate => candidate.Email == firstEmail ? secondEmail : firstEmail),
            TestContext.Current.CancellationToken);

        var stored = await Graph.Nodes<AtomicMutationNode>()
            .Where(candidate => candidate.Marker == marker)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, affected);
        Assert.Equal([firstKey, secondKey], stored.Select(candidate => candidate.KeyCode).Order());
        Assert.Equal([firstEmail, secondEmail], stored.Select(candidate => candidate.Email).Order());
    }

    [Fact]
    public async Task KeyUpdate_SelectedSetDuplicateRollsBackEverySetter()
    {
        var marker = $"command-key-duplicate-{Guid.NewGuid():N}";
        for (var index = 0; index < 2; index++)
        {
            await Graph.CreateNodeAsync(new AtomicMutationNode
            {
                KeyGroup = marker,
                KeyCode = $"key-{index}",
                Email = $"key-{index}-{Guid.NewGuid():N}@example.com",
                Marker = marker,
            }, cancellationToken: TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicMutationNode>().Where(candidate => candidate.Marker == marker),
            setters => setters
                .SetProperty(candidate => candidate.Marker, marker + "-changed")
                .SetProperty(candidate => candidate.KeyCode, "duplicate-key"),
            TestContext.Current.CancellationToken));

        var stored = await Graph.Nodes<AtomicMutationNode>()
            .Where(candidate => candidate.Marker == marker || candidate.Marker == marker + "-changed")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.All(stored, candidate => Assert.Equal(marker, candidate.Marker));
        Assert.Equal(["key-0", "key-1"], stored.Select(candidate => candidate.KeyCode).Order());
    }

    [Fact]
    public async Task UniqueUpdate_SelectedSetDuplicateRollsBackEverySetter()
    {
        var marker = $"command-constraint-duplicate-{Guid.NewGuid():N}";
        var duplicateEmail = $"duplicate-{Guid.NewGuid():N}@example.com";
        for (var index = 0; index < 2; index++)
        {
            await Graph.CreateNodeAsync(new AtomicMutationNode
            {
                KeyGroup = marker,
                KeyCode = $"key-{index}",
                Email = $"before-{index}-{Guid.NewGuid():N}@example.com",
                Marker = marker,
            }, cancellationToken: TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicMutationNode>().Where(candidate => candidate.Marker == marker),
            setters => setters
                .SetProperty(candidate => candidate.Marker, marker + "-changed")
                .SetProperty(candidate => candidate.Email, duplicateEmail),
            TestContext.Current.CancellationToken));

        var stored = await Graph.Nodes<AtomicMutationNode>()
            .Where(candidate => candidate.Marker == marker || candidate.Marker == marker + "-changed")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.All(stored, candidate => Assert.Equal(marker, candidate.Marker));
        Assert.DoesNotContain(stored, candidate => candidate.Email == duplicateEmail);
    }

    [Fact]
    public async Task UniqueUpdate_UnselectedCollisionRollsBackEverySetter()
    {
        var marker = $"command-constraint-unselected-{Guid.NewGuid():N}";
        var occupiedEmail = $"occupied-{Guid.NewGuid():N}@example.com";
        await Graph.CreateNodeAsync(new AtomicMutationNode
        {
            KeyGroup = marker,
            KeyCode = "selected",
            Email = $"selected-{Guid.NewGuid():N}@example.com",
            Marker = marker,
        }, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(new AtomicMutationNode
        {
            KeyGroup = marker,
            KeyCode = "unselected",
            Email = occupiedEmail,
            Marker = marker + "-other",
        }, cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicMutationNode>().Where(candidate => candidate.Marker == marker),
            setters => setters
                .SetProperty(candidate => candidate.Marker, marker + "-changed")
                .SetProperty(candidate => candidate.Email, occupiedEmail),
            TestContext.Current.CancellationToken));

        var selected = await Graph.Nodes<AtomicMutationNode>()
            .SingleAsync(candidate => candidate.KeyCode == "selected" && candidate.KeyGroup == marker,
                TestContext.Current.CancellationToken);
        Assert.Equal(marker, selected.Marker);
        Assert.NotEqual(occupiedEmail, selected.Email);
    }

    [Fact]
    public async Task RelationshipUniqueUpdate_AllowsValidSelectedSetSwap()
    {
        var marker = $"command-relationship-constraint-{Guid.NewGuid():N}";
        var first = new Person { FirstName = marker + "-first" };
        var second = new Person { FirstName = marker + "-second" };
        await Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        var firstCode = $"first-{Guid.NewGuid():N}";
        var secondCode = $"second-{Guid.NewGuid():N}";
        await Graph.CreateRelationshipAsync(
            new AtomicMutationRelationship(first.Id, second.Id) { Code = firstCode, Marker = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new AtomicMutationRelationship(second.Id, first.Id) { Code = secondCode, Marker = marker },
            cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Relationships<AtomicMutationRelationship>().Where(candidate => candidate.Marker == marker),
            setters => setters.SetProperty(
                candidate => candidate.Code,
                candidate => candidate.Code == firstCode ? secondCode : firstCode),
            TestContext.Current.CancellationToken);

        var stored = await Graph.Relationships<AtomicMutationRelationship>()
            .Where(candidate => candidate.Marker == marker)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, affected);
        Assert.Equal([firstCode, secondCode], stored.Select(candidate => candidate.Code).Order());
    }

    [Fact]
    public async Task OrdinaryIdUpdate_AllowsDuplicateFinalValuesOnKeylessModel()
    {
        var marker = $"command-ordinary-id-{Guid.NewGuid():N}";
        await Graph.CreateNodeAsync(
            new AtomicOrdinaryIdNode { Marker = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new AtomicOrdinaryIdNode { Marker = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        var duplicatedId = $"ordinary-{Guid.NewGuid():N}";

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<AtomicOrdinaryIdNode>().Where(candidate => candidate.Marker == marker),
            setters => setters.SetProperty(candidate => candidate.Id, duplicatedId),
            TestContext.Current.CancellationToken);

        var storedIds = await Graph.Nodes<AtomicOrdinaryIdNode>()
            .Where(candidate => candidate.Marker == marker)
            .Select(candidate => candidate.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, affected);
        Assert.Equal([duplicatedId, duplicatedId], storedIds);
    }

    [Fact]
    public async Task ZeroTargetMutations_ReturnZero()
    {
        var missing = $"missing-{Guid.NewGuid():N}";

        var updated = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>().Where(person => person.Id == missing),
            setters => setters.SetProperty(person => person.FirstName, "never"),
            TestContext.Current.CancellationToken);
        var deleted = await GraphCommandExtensions.DeleteAsync(
            Graph.Relationships<Knows>().Where(relationship => relationship.Id == missing),
            cascadeDelete: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task CancelledMutation_LeavesSelectedEntityUnchanged()
    {
        var person = new Person { FirstName = "before" };
        await Graph.CreateNodeAsync(person, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>().Where(candidate => candidate.Id == person.Id),
            setters => setters.SetProperty(candidate => candidate.FirstName, "after"),
            cancellation.Token));

        Assert.Equal(
            "before",
            (await Graph.GetNodeAsync<Person>(
                person.Id,
                cancellationToken: TestContext.Current.CancellationToken)).FirstName);
    }

    [Fact]
    [RequiresCapability(GraphCapability.FullTextSearch)]
    public async Task SearchSelection_ComposesWithPredicateBeforeMutation()
    {
        var searchTerm = $"commandsearch{Guid.NewGuid():N}";
        var selected = new Person { FirstName = searchTerm, LastName = "selected", Age = 10 };
        var excluded = new Person { FirstName = searchTerm, LastName = "excluded", Age = 20 };
        await Graph.CreateNodeAsync(selected, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(excluded, cancellationToken: TestContext.Current.CancellationToken);

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>()
                .Where(person => person.LastName == "selected")
                .Search(searchTerm),
            setters => setters.SetProperty(person => person.Age, 11),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal(
            11,
            (await Graph.GetNodeAsync<Person>(
                selected.Id,
                cancellationToken: TestContext.Current.CancellationToken)).Age);
        Assert.Equal(
            20,
            (await Graph.GetNodeAsync<Person>(
                excluded.Id,
                cancellationToken: TestContext.Current.CancellationToken)).Age);
    }

    [Fact]
    public async Task RelationshipExistenceSelection_MutatesOnlyParticipatingNodes()
    {
        var marker = $"command-exists-{Guid.NewGuid():N}";
        var selected = new Person { FirstName = "selected", LastName = marker, Age = 10 };
        var excluded = new Person { FirstName = "excluded", LastName = marker, Age = 20 };
        var endpoint = new Person { FirstName = "endpoint" };
        await Graph.CreateNodeAsync(selected, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(excluded, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(endpoint, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(selected, endpoint) { Since = DateTime.UnixEpoch.AddDays(1) },
            cancellationToken: TestContext.Current.CancellationToken);
        var threshold = DateTime.UnixEpoch;

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>()
                .Where(person => person.LastName == marker)
                .WhereHasRelationship<Person, Knows>(
                    GraphTraversalDirection.Outgoing,
                    relationship => relationship.Since >= threshold),
            setters => setters.SetProperty(person => person.Age, person => person.Age + 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        Assert.Equal(
            11,
            (await Graph.GetNodeAsync<Person>(
                selected.Id,
                cancellationToken: TestContext.Current.CancellationToken)).Age);
        Assert.Equal(
            20,
            (await Graph.GetNodeAsync<Person>(
                excluded.Id,
                cancellationToken: TestContext.Current.CancellationToken)).Age);
    }

    [Fact]
    public async Task SetBasedUpdate_AppliesOrderedWindowAndComputedValues()
    {
        var marker = $"command-window-{Guid.NewGuid():N}";
        foreach (var age in new[] { 30, 10, 20, 40 })
        {
            await Graph.CreateNodeAsync(
                new Person { FirstName = $"person-{age}", LastName = marker, Age = age },
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.Nodes<Person>()
                .Where(person => person.LastName == marker)
                .OrderBy(person => person.Age)
                .Skip(1)
                .Take(2),
            setters => setters
                .SetProperty(person => person.FirstName, "selected")
                .SetProperty(person => person.Age, person => person.Age + 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        var people = await Graph.Nodes<Person>()
            .Where(person => person.LastName == marker)
            .OrderBy(person => person.Age)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([10, 21, 31, 40], people.Select(person => person.Age));
        Assert.Equal(2, people.Count(person => person.FirstName == "selected"));
    }

    [Fact]
    public async Task SetBasedRelationshipUpdateAndDelete_UseSelectedRelationships()
    {
        var first = new Person { FirstName = "first" };
        var second = new Person { FirstName = "second" };
        var relationship = new Knows(first, second) { Since = DateTime.UnixEpoch };
        await Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(relationship, cancellationToken: TestContext.Current.CancellationToken);
        var replacement = DateTime.UnixEpoch.AddDays(1);

        var updated = await GraphCommandExtensions.UpdateAsync(
            Graph.Relationships<Knows>().Where(candidate => candidate.Id == relationship.Id),
            setters => setters.SetProperty(candidate => candidate.Since, replacement),
            TestContext.Current.CancellationToken);
        var stored = await Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        var deleted = await GraphCommandExtensions.DeleteAsync(
            Graph.Relationships<Knows>().Where(candidate => candidate.Id == relationship.Id),
            cascadeDelete: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, updated);
        Assert.Equal(replacement, stored.Since);
        Assert.Equal(1, deleted);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetBasedNodeDelete_PreflightsAtomicallyAndCascadeRemovesOnlyOwnedSubgraph()
    {
        var marker = $"command-delete-{Guid.NewGuid():N}";
        var complexMarker = $"owned-{Guid.NewGuid():N}";
        var first = new PersonWithComplexProperty
        {
            FirstName = "first",
            LastName = marker,
            Address = new AddressValue { Street = complexMarker, City = complexMarker },
        };
        var second = new PersonWithComplexProperty
        {
            FirstName = "second",
            LastName = marker,
            Address = new AddressValue { Street = complexMarker, City = complexMarker },
        };
        var survivor = new Person { FirstName = "survivor" };
        var relationship = new Knows(first, survivor);
        await Graph.CreateNodeAsync(first, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(survivor, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(relationship, cancellationToken: TestContext.Current.CancellationToken);
        var targets = Graph.Nodes<PersonWithComplexProperty>().Where(person => person.LastName == marker);

        await Assert.ThrowsAsync<GraphException>(() => GraphCommandExtensions.DeleteAsync(
            targets,
            cascadeDelete: false,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, await targets.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            2,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(AddressValue)),
                nameof(AddressValue.City),
                [complexMarker],
                TestContext.Current.CancellationToken));

        var affected = await GraphCommandExtensions.DeleteAsync(
            targets,
            cascadeDelete: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        Assert.Equal(0, await targets.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            0,
            await Harness.CountNodesByPropertyAsync(
                Graph,
                Labels.GetLabelFromType(typeof(AddressValue)),
                nameof(AddressValue.City),
                [complexMarker],
                TestContext.Current.CancellationToken));
        _ = await Graph.GetNodeAsync<Person>(
            survivor.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Graph.GetRelationshipAsync<Knows>(
            relationship.Id,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExactOneEndpointSelection_ThrowsRoleSpecificCardinalityFailures()
    {
        var marker = $"command-exact-one-{Guid.NewGuid():N}";
        var missing = $"missing-{Guid.NewGuid():N}";
        await Graph.CreateNodeAsync(
            new Person { FirstName = "first", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new Person { FirstName = "second", LastName = marker },
            cancellationToken: TestContext.Current.CancellationToken);
        var empty = Graph.Nodes<Person>().Where(person => person.LastName == missing);
        var multiple = Graph.Nodes<Person>().Where(person => person.LastName == marker);
        var provider = Assert.IsAssignableFrom<IGraphCommandProvider>(multiple.Provider);

        var emptyFailure = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            provider.InWriteTransactionAsync(
                (context, token) => GraphCommandSelection.SelectExactOneAsync(
                    context,
                    new GraphElementSelectionModel(
                        GraphQueryModelBuilder.Build(empty.Expression),
                        GraphElementSelectionMode.ExactOne),
                    empty.Expression,
                    GraphEndpointRole.Source,
                    token),
                TestContext.Current.CancellationToken));
        var multipleFailure = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            provider.InWriteTransactionAsync(
                (context, token) => GraphCommandSelection.SelectExactOneAsync(
                    context,
                    new GraphElementSelectionModel(
                        GraphQueryModelBuilder.Build(multiple.Expression),
                        GraphElementSelectionMode.ExactOne),
                    multiple.Expression,
                    GraphEndpointRole.Target,
                    token),
                TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Source, emptyFailure.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, emptyFailure.Failure);
        Assert.Equal(GraphEndpointRole.Target, multipleFailure.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, multipleFailure.Failure);
    }

    [Fact]
    public async Task SetBasedDynamicUpdate_DoesNotExposeProviderNativeIdentity()
    {
        var label = $"CommandDynamic{Guid.NewGuid():N}";
        var originalNames = new[] { "old" };
        var node = new DynamicNode(
            [label],
            new Dictionary<string, object?> { ["score"] = 1, ["names"] = originalNames });
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var replacement = new[] { "first", "second" };

        var affected = await GraphCommandExtensions.UpdateAsync(
            Graph.DynamicNodes().Where(candidate => candidate.Id == node.Id),
            setters => setters
                .SetProperty(candidate => candidate.Properties["score"], 2)
                .SetProperty(candidate => candidate.Properties["names"], replacement),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, affected);
        var updated = await Graph.GetDynamicNodeAsync(
            node.Id,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2L, Convert.ToInt64(updated.Properties["score"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(
            replacement,
            Assert.IsAssignableFrom<System.Collections.IEnumerable>(updated.Properties["names"])
                .Cast<object?>()
                .Cast<string>());
        Assert.DoesNotContain("__nativeId", updated.Properties.Keys);
    }
}

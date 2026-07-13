// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract tests for <see cref="IGraph.CreateAsync{TSource, TRelationship, TTarget}"/>: the atomic
/// node–relationship–node subgraph create. Every provider inherits these.
/// </summary>
public interface ISubgraphCreationTests : IGraphTest
{
    public sealed record MarkerCollisionNode : Node
    {
        [Property(Label = "__graphModelSubgraphCreated")]
        public bool ReservedLookingProperty { get; set; }

        public AddressValue Address { get; set; } = new();
    }

    [Fact]
    public async Task CreateSubgraph_CreatesBothNodesAndRelationship()
    {
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id, Since = DateTime.UtcNow };

        await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken);

        var fetchedSource = await Graph.GetNodeAsync<Person>(source.Id, null, TestContext.Current.CancellationToken);
        var fetchedTarget = await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken);
        var fetchedRel = await Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Alice", fetchedSource.FirstName);
        Assert.Equal("Bob", fetchedTarget.FirstName);
        Assert.Equal(source.Id, fetchedRel.StartNodeId);
        Assert.Equal(target.Id, fetchedRel.EndNodeId);
    }

    [Fact]
    public async Task CreateSubgraph_IncomingDirection_RoundTrips()
    {
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows
        {
            StartNodeId = source.Id,
            EndNodeId = target.Id,
            Direction = RelationshipDirection.Incoming,
            Since = DateTime.UtcNow
        };

        await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken);

        var fetchedRel = await Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(source.Id, fetchedRel.StartNodeId);
        Assert.Equal(target.Id, fetchedRel.EndNodeId);
        Assert.Equal(RelationshipDirection.Incoming, fetchedRel.Direction);
    }

    [Fact]
    public async Task CreateSubgraph_EndpointIdMismatch_Throws()
    {
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        // StartNodeId does not match the source node's Id.
        var knows = new Knows { StartNodeId = Guid.NewGuid().ToString("N"), EndNodeId = target.Id };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken));

        // Nothing was created.
        var people = await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(people);
    }

    [Fact]
    public async Task CreateSubgraph_DuplicateEndpoint_CreatesNothing()
    {
        var source = new Person { FirstName = "Alice" };
        await Graph.CreateNodeAsync(source, null, TestContext.Current.CancellationToken);

        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        // Default semantics CREATE both endpoints, so the existing source id fails the whole
        // statement atomically.
        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken));

        // The target and the edge were rolled back; only the pre-existing source remains.
        var people = await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(people);
        Assert.Equal(source.Id, people[0].Id);

        var relationships = await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(relationships);

        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSubgraph_CreateMissingEndpoints_ReusesExistingEndpoint()
    {
        // Pre-create the source with a distinctive value that must survive the merge unchanged.
        var existingSource = new Person { FirstName = "Original" };
        await Graph.CreateNodeAsync(existingSource, null, TestContext.Current.CancellationToken);

        // Same Id, different properties: a merge must not clobber the stored node.
        var source = new Person { Id = existingSource.Id, FirstName = "Changed" };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };
        var options = new GraphOperationOptions { CreateMissingEndpoints = true };

        await Graph.CreateAsync(source, knows, target, options, null, TestContext.Current.CancellationToken);

        // The existing endpoint is reused as-is (not clobbered, not duplicated).
        var fetchedSource = await Graph.GetNodeAsync<Person>(source.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Original", fetchedSource.FirstName);

        var sourcesWithId = await Graph.Nodes<Person>()
            .Where(p => p.Id == source.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(sourcesWithId);

        // The missing endpoint and the edge were created.
        var fetchedTarget = await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Bob", fetchedTarget.FirstName);

        var fetchedRel = await Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(source.Id, fetchedRel.StartNodeId);
        Assert.Equal(target.Id, fetchedRel.EndNodeId);
    }

    [Fact]
    public async Task CreateSubgraph_CreateMissingEndpoints_MatchedEndpointComplexSubtreeNotDuplicated()
    {
        // Pre-create an endpoint carrying a *collection* complex property, so a duplicated subtree
        // would be observable as a doubled element count on read-back. Parity lock (#45): all three
        // providers must reuse a matched endpoint entirely as-is, leaving its existing subtree alone.
        var existing = new Kennel
        {
            Name = "Downtown",
            Animals =
            {
                new AnimalDescription { Name = "Rex" },
                new AnimalDescription { Name = "Fido" },
                new AnimalDescription { Name = "Whiskers" }
            }
        };
        await Graph.CreateNodeAsync(existing, null, TestContext.Current.CancellationToken);

        // Same Id, complex property re-populated: the merge must ignore the passed-in properties and
        // NOT re-create the subtree for the matched endpoint.
        var source = new Kennel
        {
            Id = existing.Id,
            Name = "Changed",
            Animals = { new AnimalDescription { Name = "Ignored" } }
        };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };
        var options = new GraphOperationOptions { CreateMissingEndpoints = true };

        await Graph.CreateAsync(source, knows, target, options, null, TestContext.Current.CancellationToken);

        // The pre-existing endpoint is reused as-is: exactly one subtree, unchanged shape (not doubled,
        // not clobbered by the passed-in "Ignored"/"Changed" values).
        var fetchedSource = await Graph.GetNodeAsync<Kennel>(source.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Downtown", fetchedSource.Name);
        Assert.Equal(3, fetchedSource.Animals.Count);
        Assert.Equal(
            ["Fido", "Rex", "Whiskers"],
            fetchedSource.Animals.Select(a => a.Name).OrderBy(name => name).ToArray());

        var kennelsWithId = await Graph.Nodes<Kennel>()
            .Where(k => k.Id == source.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(kennelsWithId);

        // The missing (brand-new) endpoint and the edge were created.
        var fetchedTarget = await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Bob", fetchedTarget.FirstName);

        var fetchedRel = await Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(source.Id, fetchedRel.StartNodeId);
        Assert.Equal(target.Id, fetchedRel.EndNodeId);
    }

    [Fact]
    public async Task CreateSubgraph_ComplexPropertiesOnEndpoints_RoundTrip()
    {
        var source = new PersonWithComplexProperty
        {
            FirstName = "Alice",
            Address = new AddressValue { Street = "1 Source St", City = "Sourceville" }
        };
        var target = new PersonWithComplexProperty
        {
            FirstName = "Bob",
            Address = new AddressValue { Street = "2 Target Ave", City = "Targettown" }
        };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken);

        var fetchedSource = await Graph.GetNodeAsync<PersonWithComplexProperty>(source.Id, null, TestContext.Current.CancellationToken);
        var fetchedTarget = await Graph.GetNodeAsync<PersonWithComplexProperty>(target.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("1 Source St", fetchedSource.Address.Street);
        Assert.Equal("Sourceville", fetchedSource.Address.City);
        Assert.Equal("2 Target Ave", fetchedTarget.Address.Street);
        Assert.Equal("Targettown", fetchedTarget.Address.City);
    }

    [Fact]
    public async Task CreateSubgraph_ValidatesEndpointProperties()
    {
        var source = new IAttributeValidationTests.PersonWithRequiredProperties
        {
            FirstName = string.Empty,
            LastName = string.Empty
        };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSubgraph_CallerTransactionFailureLeavesNoPartialEndpoints()
    {
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        var invalidRelationship = new IAttributeValidationTests.RelationshipWithValidationProperties(
            source.Id,
            target.Id)
        {
            Notes = string.Empty
        };

        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(
                source,
                invalidRelationship,
                target,
                null,
                transaction,
                TestContext.Current.CancellationToken));

        // The caller still owns a usable transaction. Committing it must not persist endpoints from
        // the failed subgraph operation.
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(source.Id, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Person>(target.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSubgraph_CreateMissingEndpoints_MatchedEndpointMarkerPropertyRemainsUntouched()
    {
        var existing = new MarkerCollisionNode
        {
            ReservedLookingProperty = true,
            Address = new AddressValue { Street = "Original St", City = "Original City" }
        };
        await Graph.CreateNodeAsync(existing, null, TestContext.Current.CancellationToken);

        var source = new MarkerCollisionNode
        {
            Id = existing.Id,
            ReservedLookingProperty = false,
            Address = new AddressValue { Street = "Ignored St", City = "Ignored City" }
        };
        var target = new MarkerCollisionNode
        {
            ReservedLookingProperty = true,
            Address = new AddressValue { Street = "New St", City = "New City" }
        };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        await Graph.CreateAsync(
            source,
            knows,
            target,
            new GraphOperationOptions { CreateMissingEndpoints = true },
            null,
            TestContext.Current.CancellationToken);

        var fetched = await Graph.GetNodeAsync<MarkerCollisionNode>(
            existing.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.True(fetched.ReservedLookingProperty);
        Assert.Equal("Original St", fetched.Address.Street);
        Assert.Equal("Original City", fetched.Address.City);

        var fetchedTarget = await Graph.GetNodeAsync<MarkerCollisionNode>(
            target.Id,
            null,
            TestContext.Current.CancellationToken);
        Assert.True(fetchedTarget.ReservedLookingProperty);
        Assert.Equal("New St", fetchedTarget.Address.Street);
        Assert.Equal("New City", fetchedTarget.Address.City);
    }
}

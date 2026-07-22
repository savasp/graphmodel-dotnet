// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>Contract tests for atomic endpoint-intent relationship creation.</summary>
public interface ISubgraphCreationTests : IGraphTest
{
    public sealed record KeylessEndpoint : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task AllNewCreate_CreatesBothNodesAndRelationship()
    {
        var source = new KeylessEndpoint { Name = "all-new-source" };
        var target = new KeylessEndpoint { Name = "all-new-target" };
        var relationship = new Knows { Since = DateTime.UtcNow };

        await Graph.CreateAsync(
            source,
            relationship,
            target,
            RelationshipDirection.Outgoing,
            cancellationToken: TestContext.Current.CancellationToken);

        var nodes = await Graph.Nodes<KeylessEndpoint>()
            .ToListAsync(TestContext.Current.CancellationToken);
        var storedRelationship = await Graph.Relationships<Knows>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal([source.Name, target.Name], nodes.Select(node => node.Name).Order());
        Assert.Equal(relationship.Since, storedRelationship.Since);
    }

    [Fact]
    public async Task AllNewCreate_IncomingDirectionAppearsOnPathSegment()
    {
        var source = new KeylessEndpoint { Name = "incoming-source" };
        var target = new KeylessEndpoint { Name = "incoming-target" };

        await Graph.CreateAsync(
            source,
            new Knows(),
            target,
            RelationshipDirection.Incoming,
            cancellationToken: TestContext.Current.CancellationToken);

        var segment = await Graph.Nodes<KeylessEndpoint>()
            .Where(node => node.Name == source.Name)
            .PathSegments<KeylessEndpoint, Knows, KeylessEndpoint>(GraphTraversalDirection.Both)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(source.Name, segment.StartNode.Name);
        Assert.Equal(target.Name, segment.EndNode.Name);
        Assert.Equal(RelationshipDirection.Incoming, segment.Direction);
    }

    [Fact]
    public async Task SelectedSelectedCreate_ConnectsExistingEndpoints()
    {
        var source = new Person { FirstName = "selected-source" };
        var target = new Person { FirstName = "selected-target" };
        await Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(
            Graph.SelectNode(source),
            new Knows(),
            Graph.SelectNode(target),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await Graph.Nodes<Person>().CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedNewCreate_ReusesSourceAndCreatesTarget()
    {
        var source = new Person { FirstName = "selected-source" };
        var target = new Person { FirstName = "new-target" };
        await Graph.CreateNodeAsync(source, cancellationToken: TestContext.Current.CancellationToken);

        await Graph.CreateAsync(
            Graph.SelectNode(source),
            new Knows(),
            target,
            cancellationToken: TestContext.Current.CancellationToken);

        var people = await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, people.Count);
        Assert.Single(people, person => person.TestKey == source.TestKey);
        Assert.Single(people, person => person.TestKey == target.TestKey);
        Assert.Single(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NewSelectedCreate_CreatesSourceAndReusesTarget()
    {
        var source = new Person { FirstName = "new-source" };
        var target = new Person { FirstName = "selected-target" };
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);

        await Graph.CreateAsync(
            source,
            new Knows(),
            Graph.SelectNode(target),
            cancellationToken: TestContext.Current.CancellationToken);

        var people = await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, people.Count);
        Assert.Single(people, person => person.TestKey == source.TestKey);
        Assert.Single(people, person => person.TestKey == target.TestKey);
        Assert.Single(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllNewSelfLoop_CreatesOneKeylessNode()
    {
        var node = new KeylessEndpoint { Name = "new-self-loop" };

        await Graph.CreateSelfLoopAsync(
            node,
            new Knows(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(await Graph.Nodes<KeylessEndpoint>().ToListAsync(TestContext.Current.CancellationToken));
        var segment = await Graph.Nodes<KeylessEndpoint>()
            .PathSegments<KeylessEndpoint, Knows, KeylessEndpoint>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(node.Name, segment.StartNode.Name);
        Assert.Equal(node.Name, segment.EndNode.Name);
    }

    [Fact]
    public async Task ExistingSelfLoop_UsesSameExactOneSelectionForBothEndpoints()
    {
        var node = new Person { FirstName = "existing-self-loop" };
        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var selected = Graph.SelectNode(node);

        await Graph.CreateRelationshipAsync(
            selected,
            new Knows(),
            selected,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedEndpoint_EmptyThrowsTypedCardinalityFailure()
    {
        var target = new Person { FirstName = "target" };
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            Graph.CreateRelationshipAsync(
                Graph.Nodes<Person>().Where(person => person.FirstName == "missing"),
                new Knows(),
                Graph.SelectNode(target),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Source, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, exception.Failure);
        Assert.Empty(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedEndpoint_MultipleThrowsTypedCardinalityFailure()
    {
        await Graph.CreateNodeAsync(
            new Person { FirstName = "duplicate" },
            cancellationToken: TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(
            new Person { FirstName = "duplicate" },
            cancellationToken: TestContext.Current.CancellationToken);
        var target = new Person { FirstName = "target" };
        await Graph.CreateNodeAsync(target, cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            Graph.CreateRelationshipAsync(
                Graph.Nodes<Person>().Where(person => person.FirstName == "duplicate"),
                new Knows(),
                Graph.SelectNode(target),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Source, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Multiple, exception.Failure);
        Assert.Empty(await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllNewCreate_ValidationFailureRollsBackEndpointsInCallerTransaction()
    {
        var source = new Person { FirstName = "invalid-source" };
        var target = new Person { FirstName = "invalid-target" };
        var invalidRelationship = new IAttributeValidationTests.RelationshipWithValidationProperties
        {
            Notes = string.Empty,
        };
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(() => Graph.CreateAsync(
            source,
            invalidRelationship,
            target,
            RelationshipDirection.Outgoing,
            transaction,
            TestContext.Current.CancellationToken));
        await transaction.CommitAsync();

        Assert.Empty(await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Graph.Relationships<IAttributeValidationTests.RelationshipWithValidationProperties>()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AllNewCreate_ComplexEndpointPropertiesRoundTrip()
    {
        var source = new PersonWithComplexProperty
        {
            FirstName = "complex-source",
            Address = new AddressValue { Street = "1 Source St", City = "Sourceville" },
        };
        var target = new PersonWithComplexProperty
        {
            FirstName = "complex-target",
            Address = new AddressValue { Street = "2 Target Ave", City = "Targettown" },
        };

        await Graph.CreateAsync(
            source,
            new Knows(),
            target,
            cancellationToken: TestContext.Current.CancellationToken);

        var people = await Graph.Nodes<PersonWithComplexProperty>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(people, person => person.Address.Street == source.Address.Street);
        Assert.Contains(people, person => person.Address.Street == target.Address.Street);
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests.GraphTests;

using Cvoya.Graph.CompatibilityTests;

/// <summary>
/// AGE-specific end-to-end tests for the structured correlated-projection and pattern-subquery
/// lowering (<c>AgeCorrelatedProjectionPass</c>). These pin result values, not rendered Cypher, for
/// the shapes the shared compatibility contract does not exercise: filtered correlated aggregates,
/// segment-filtered groupings, and existence/count predicates in <c>Where</c> — all of which AGE
/// silently mis-executes when rendered natively.
/// </summary>
public sealed class AgeCorrelatedProjectionIntegrationTests(AgeHarness harness) : AgeTest(harness)
{
    [Fact]
    public async Task FilteredCorrelatedAggregate_AggregatesOnlyMatchingRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice", Age = 40 };
        var kid = new Person { FirstName = "Kid", Age = 10 };
        var adult = new Person { FirstName = "Adult", Age = 30 };
        await this.Graph.CreateNodeAsync(alice, null, ct);
        await this.Graph.CreateNodeAsync(kid, null, ct);
        await this.Graph.CreateNodeAsync(adult, null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, kid), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, adult), null, ct);

        var row = await this.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                AverageAdultAge = group
                    .Where(segment => segment.EndNode.Age >= 18)
                    .Average(segment => segment.EndNode.Age),
            })
            .SingleAsync(ct);

        Assert.Equal("Alice", row.Name);
        Assert.Equal(30.0, row.AverageAdultAge);
    }

    [Fact]
    public async Task FilteredOrderedCollection_CollectsOnlyMatchingRowsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice", Age = 40 };
        var kid = new Person { FirstName = "Kid", Age = 10 };
        var senior = new Person { FirstName = "Senior", Age = 50 };
        var adult = new Person { FirstName = "Adult", Age = 30 };
        await this.Graph.CreateNodeAsync(alice, null, ct);
        await this.Graph.CreateNodeAsync(kid, null, ct);
        await this.Graph.CreateNodeAsync(senior, null, ct);
        await this.Graph.CreateNodeAsync(adult, null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, kid), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, senior), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, adult), null, ct);

        var row = await this.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                AdultFriends = group
                    .Where(segment => segment.EndNode.Age >= 18)
                    .OrderBy(segment => segment.EndNode.Age)
                    .Select(segment => segment.EndNode.FirstName)
                    .ToList(),
            })
            .SingleAsync(ct);

        Assert.Equal(["Adult", "Senior"], row.AdultFriends);
    }

    [Fact]
    public async Task SegmentFilteredGrouping_DropsOwnersWithoutQualifyingRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice", Age = 40 };
        var bob = new Person { FirstName = "Bob", Age = 40 };
        var kid = new Person { FirstName = "Kid", Age = 10 };
        var adult = new Person { FirstName = "Adult", Age = 30 };
        await this.Graph.CreateNodeAsync(alice, null, ct);
        await this.Graph.CreateNodeAsync(bob, null, ct);
        await this.Graph.CreateNodeAsync(kid, null, ct);
        await this.Graph.CreateNodeAsync(adult, null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, kid), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, adult), null, ct);

        var rows = await this.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .Where(segment => segment.EndNode.Age >= 18)
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                Name = group.Key.FirstName,
                Friends = group.Select(segment => segment.EndNode.FirstName).ToList(),
            })
            .ToListAsync(ct);

        // Alice's only segment fails the filter, so LINQ yields no group for her at all.
        var row = Assert.Single(rows);
        Assert.Equal("Bob", row.Name);
        Assert.Equal(["Adult"], row.Friends);
    }

    [Fact]
    public async Task ExistenceFilter_ReturnsOnlyOwnersWithMatchingElements()
    {
        var ct = TestContext.Current.CancellationToken;
        var happy = new Kennel
        {
            Name = "Happy Paws",
            Animals = [new AnimalDescription { Name = "Rex" }, new AnimalDescription { Name = "Bella" }],
        };
        var other = new Kennel { Name = "Other", Animals = [new AnimalDescription { Name = "Milo" }] };
        var empty = new Kennel { Name = "Empty", Animals = [] };
        await this.Graph.CreateNodeAsync(happy, null, ct);
        await this.Graph.CreateNodeAsync(other, null, ct);
        await this.Graph.CreateNodeAsync(empty, null, ct);

        var names = await this.Graph.Nodes<Kennel>()
            .Where(kennel => kennel.Animals.Any(animal => animal.Name == "Rex"))
            .Select(kennel => kennel.Name)
            .ToListAsync(ct);

        Assert.Equal(["Happy Paws"], names);
    }

    [Fact]
    public async Task CountFilter_ComparesCollectionSizes()
    {
        var ct = TestContext.Current.CancellationToken;
        var happy = new Kennel
        {
            Name = "Happy Paws",
            Animals = [new AnimalDescription { Name = "Rex" }, new AnimalDescription { Name = "Bella" }],
        };
        var other = new Kennel { Name = "Other", Animals = [new AnimalDescription { Name = "Milo" }] };
        await this.Graph.CreateNodeAsync(happy, null, ct);
        await this.Graph.CreateNodeAsync(other, null, ct);

        var names = await this.Graph.Nodes<Kennel>()
            .Where(kennel => kennel.Animals.Count > 1)
            .Select(kennel => kennel.Name)
            .ToListAsync(ct);

        Assert.Equal(["Happy Paws"], names);
    }

    [Fact]
    public async Task DegreeFilter_ComparesRelationshipCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        await this.Graph.CreateNodeAsync(alice, null, ct);
        await this.Graph.CreateNodeAsync(bob, null, ct);
        await this.Graph.CreateNodeAsync(charlie, null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, ct);

        var names = await this.Graph.Nodes<Person>()
            .Where(person => person.CountRelationships<Knows>(GraphTraversalDirection.Outgoing) > 1)
            .Select(person => person.FirstName)
            .ToListAsync(ct);

        Assert.Equal(["Alice"], names);
    }

    [Fact]
    public async Task ExistenceFilterWithSizeProjection_CountsAllElementsOfMatchingOwners()
    {
        var ct = TestContext.Current.CancellationToken;
        var happy = new Kennel
        {
            Name = "Happy Paws",
            Animals = [new AnimalDescription { Name = "Rex" }, new AnimalDescription { Name = "Bella" }],
        };
        var other = new Kennel { Name = "Other", Animals = [new AnimalDescription { Name = "Milo" }] };
        await this.Graph.CreateNodeAsync(happy, null, ct);
        await this.Graph.CreateNodeAsync(other, null, ct);

        var row = await this.Graph.Nodes<Kennel>()
            .Where(kennel => kennel.Animals.Any(animal => animal.Name == "Rex"))
            .Select(kennel => new { kennel.Name, Size = kennel.Animals.Count })
            .SingleAsync(ct);

        // The filter selects the kennel; the size still counts every animal, not just "Rex".
        Assert.Equal("Happy Paws", row.Name);
        Assert.Equal(2, row.Size);
    }

    [Fact]
    public async Task GroupKeyDegreeCount_ProjectsAlongsideCorrelatedCollection()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var address = new Address { Street = "1 Main St", City = "Springfield" };
        await this.Graph.CreateNodeAsync(alice, null, ct);
        await this.Graph.CreateNodeAsync(bob, null, ct);
        await this.Graph.CreateNodeAsync(address, null, ct);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, ct);
        await this.Graph.CreateRelationshipAsync(new LivesAt(alice, address), null, ct);

        var row = await this.Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .GroupBy(segment => segment.StartNode)
            .Select(group => new
            {
                FriendNames = group.Select(segment => segment.EndNode.FirstName).ToList(),
                LivesAtCount = group.Key.CountRelationships<LivesAt>(GraphTraversalDirection.Outgoing),
            })
            .SingleAsync(ct);

        Assert.Equal(["Bob"], row.FriendNames);
        Assert.Equal(1, row.LivesAtCount);
    }
}

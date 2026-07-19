// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IQueryTraversalTests : IGraphTest
{
    #region Basic Traversal Tests

    [Fact]
    public async Task CanGetPathSegments()
    {
        // Setup: Alice knows Bob and Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) };
        var aliceKnowsCharlie = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-1) };

        await Graph.CreateRelationshipAsync(aliceKnowsBob, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(aliceKnowsCharlie, null, TestContext.Current.CancellationToken);

        // Act: Traverse from Alice to people she knows
        var knownPeople = await Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, knownPeople.Count);
        Assert.Equal("Alice", knownPeople[0].StartNode.FirstName);
        Assert.Equal("Alice", knownPeople[1].StartNode.FirstName);
        Assert.Contains(knownPeople, p => p.EndNode.FirstName == "Bob");
        Assert.Contains(knownPeople, p => p.EndNode.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanTraverseToConnectedNodes()
    {
        // Setup: Alice knows Bob and Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) };
        var aliceKnowsCharlie = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-1) };

        await Graph.CreateRelationshipAsync(aliceKnowsBob, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(aliceKnowsCharlie, null, TestContext.Current.CancellationToken);

        // Act: Traverse from Alice to people she knows
        var knownPeople = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, knownPeople.Count);
        Assert.Contains(knownPeople, p => p.FirstName == "Bob");
        Assert.Contains(knownPeople, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanTraverseWithDepthControl()
    {
        // Setup: Alice -> Bob -> Charlie (chain)
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };
        var bobKnowsCharlie = new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow };

        await Graph.CreateRelationshipAsync(aliceKnowsBob, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(bobKnowsCharlie, null, TestContext.Current.CancellationToken);

        // Act & Assert: Depth 1 - should only get Bob
        var depth1Results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(depth1Results);
        Assert.Equal("Bob", depth1Results[0].FirstName);

        // Act & Assert: Depth 2 - should get Bob and Charlie
        var depth2Results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, depth2Results.Count);
        Assert.Contains(depth2Results, p => p.FirstName == "Bob");
        Assert.Contains(depth2Results, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task CanTraverseWithDepthRange()
    {
        // Setup: Create a multi-level network: Alice -> Bob -> Charlie -> David
        var alice = new Person { FirstName = "Alice", LastName = "A" };
        var bob = new Person { FirstName = "Bob", LastName = "B" };
        var charlie = new Person { FirstName = "Charlie", LastName = "C" };
        var david = new Person { FirstName = "David", LastName = "D" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(david, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = david.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Traverse with depth range 2-3 (should get Charlie and David)
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(2, 3)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.FirstName == "David");
        Assert.Contains(results, p => p.FirstName == "Charlie");
        Assert.DoesNotContain(results, p => p.FirstName == "Bob");
        Assert.DoesNotContain(results, p => p.FirstName == "Alice");

        // Act: Traverse with depth range 1-3 (should get Bob, Charlie, and David)
        results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(1, 3)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, p => p.FirstName == "Bob");
        Assert.Contains(results, p => p.FirstName == "Charlie");
        Assert.Contains(results, p => p.FirstName == "David");
        Assert.DoesNotContain(results, p => p.FirstName == "Alice");
    }

    #endregion

    #region Direction Control Tests

    [Fact]
    public async Task CanTraverseInOutgoingDirection()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Traverse outgoing from Alice (should only get Bob)
        var outgoingResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(GraphTraversalDirection.Outgoing)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(outgoingResults);
        Assert.Equal("Bob", outgoingResults[0].FirstName);
    }

    [Fact]
    public async Task CanTraverseInIncomingDirection()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Traverse incoming to Alice (should only get Charlie)
        var incomingResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(incomingResults);
        Assert.Equal("Charlie", incomingResults[0].FirstName);
    }

    [Fact]
    public async Task CanTraverseInBothDirections()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Traverse in both directions from Alice (should get both Bob and Charlie)
        var bothDirectionsResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(GraphTraversalDirection.Both)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, bothDirectionsResults.Count);
        Assert.Contains(bothDirectionsResults, p => p.FirstName == "Bob");
        Assert.Contains(bothDirectionsResults, p => p.FirstName == "Charlie");
    }

    [Fact]
    public async Task Traversal_DirectionBoth_MatchesEitherStoredDirection()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows
        {
            StartNodeId = alice.Id,
            EndNodeId = bob.Id,
            Direction = RelationshipDirection.Outgoing,
            Since = DateTime.UtcNow
        }, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows
        {
            StartNodeId = alice.Id,
            EndNodeId = charlie.Id,
            Direction = RelationshipDirection.Incoming,
            Since = DateTime.UtcNow
        }, null, TestContext.Current.CancellationToken);

        var results = await Graph.Nodes<Person>()
            .Where(p => p.Id == alice.Id)
            .Traverse<Knows, Person>(GraphTraversalDirection.Both)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.Id == bob.Id);
        Assert.Contains(results, p => p.Id == charlie.Id);
    }

    #endregion

    #region Reverse Traversal Tests

    [Fact]
    public async Task CanReverseTraverseWithReverseTraverseMethod()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Reverse traverse from Alice to find who knows her (should get Charlie)
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .ReverseTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(reverseResults);
        Assert.Equal("Charlie", reverseResults[0].FirstName);
    }

    [Fact]
    public async Task CanReverseTraverseWithPathSegmentsDirection()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Use PathSegments with incoming direction to find who knows Alice
        var pathSegments = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(pathSegments);
        Assert.Equal("Alice", pathSegments[0].StartNode.FirstName);
        Assert.Equal("Charlie", pathSegments[0].EndNode.FirstName);
    }

    [Fact]
    public async Task CanReverseTraverseWithTraverseDirection()
    {
        // Setup: Alice knows Bob, Charlie knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Use Traverse with incoming direction to find who knows Alice
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(reverseResults);
        Assert.Equal("Charlie", reverseResults[0].FirstName);
    }

    [Fact]
    public async Task CanReverseTraverseWithMultipleIncomingRelationships()
    {
        // Setup: Alice knows Bob, Charlie knows Alice, David knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };
        var david = new Person { FirstName = "David", LastName = "Wilson" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(david, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = david.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Reverse traverse from Alice to find who knows her
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .ReverseTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, reverseResults.Count);
        Assert.Contains(reverseResults, p => p.FirstName == "Charlie");
        Assert.Contains(reverseResults, p => p.FirstName == "David");
        Assert.DoesNotContain(reverseResults, p => p.FirstName == "Bob");
    }

    [Fact]
    public async Task CanReverseTraverseWithNoIncomingRelationships()
    {
        // Setup: Alice knows Bob, but no one knows Alice
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Reverse traverse from Alice to find who knows her
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .ReverseTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(reverseResults);
    }

    [Fact]
    public async Task CanReverseTraverseWithDifferentRelationshipTypes()
    {
        // Setup: Alice knows Bob, Charlie lives at Alice's address
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new LivesAt { StartNodeId = charlie.Id, EndNodeId = alice.Id, MovedInDate = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Reverse traverse from Alice using LivesAt relationship
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .ReverseTraverse<LivesAt, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(reverseResults);
        Assert.Equal("Charlie", reverseResults[0].FirstName);
    }

    [Fact]
    public async Task CanReverseTraverseWithDepthConstraints()
    {
        // Setup: Alice knows Bob, Bob knows Charlie, Charlie knows David
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };
        var david = new Person { FirstName = "David", LastName = "Wilson" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(david, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = david.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Reverse traverse from Alice (single hop, the only depth ReverseTraverse supports)
        var reverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .ReverseTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert - Alice has no incoming relationships, so should be empty
        Assert.Empty(reverseResults);

        // Act: Reverse traverse from Bob (single hop, the only depth ReverseTraverse supports)
        var bobReverseResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Bob")
            .ReverseTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert - Bob should have Alice as incoming
        Assert.Single(bobReverseResults);
        Assert.Equal("Alice", bobReverseResults[0].FirstName);
    }

    #endregion

    #region Relationship Filtering Tests

    [Fact]
    public async Task CanFilterRelationshipsByProperty()
    {
        // Setup: Alice knows Bob (recent) and Charlie (old friendship)
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(oldKnows, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice has known for less than a year
        var recentFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since > DateTime.UtcNow.AddYears(-1))
            .Select(p => p.EndNode)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(recentFriends);
        Assert.Equal("Bob", recentFriends[0].FirstName);
    }

    [Fact]
    public async Task CanFilterTargetNodesByPropertyEmpty()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows directly who are over 50
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .Where(p => p.Age > 50)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        // We shouldn't find any friends over 50 directly connected to Alice
        Assert.Empty(olderFriends);
    }

    [Fact]
    public async Task CanFilterTargetNodesByPropertyOneHop()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows directly who are over 25
        // This should return Bob (30) and Elen (45)
        // Charlie isn't directly connected to Alice, so he won't be included
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .Where(p => p.Age > 25)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert (order is not guaranteed without OrderBy)
        Assert.Equal(2, olderFriends.Count);
        Assert.Contains(olderFriends, p => p.FirstName == "Bob");
        Assert.Contains(olderFriends, p => p.FirstName == "Elen");
    }

    [Fact]
    public async Task CanFilterTargetNodesByPropertyTwoHops()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows with two hops who are over 35
        // We should get Charlie (40) through Bob, and Elen directly

        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(2)
            .Where(p => p.Age > 35)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, olderFriends.Count);
        Assert.Contains(olderFriends, p => p.FirstName == "Charlie");
        Assert.Contains(olderFriends, p => p.FirstName == "Elen");
    }

    [Fact]
    public async Task CanFilterTargetNodesByPropertyWithoutTraverseEmpty()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows who are over 35 with one hop. Alice's direct (one-hop)
        // connections are Bob (30) and Elen (45) - Charlie (40) is two hops away via Bob, so
        // PathSegments (single-hop) must not surface him here; only Elen clears the age filter.
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(ps => ps.EndNode.Age > 35)
            .Select(ps => ps.EndNode)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(olderFriends);
        Assert.Equal("Elen", olderFriends[0].FirstName);
    }


    [Fact]
    public async Task CanFilterTargetNodesByPropertyWithoutTraverseOneHope()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows directly who are over 25
        // This should return Bob (30) and Elen (45)
        // Charlie isn't directly connected to Alice, so he won't be included
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(ps => ps.EndNode.Age > 25)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert (order is not guaranteed without OrderBy)
        Assert.Equal(2, olderFriends.Count);
        Assert.Contains(olderFriends, ps => ps.EndNode.FirstName == "Bob");
        Assert.Contains(olderFriends, ps => ps.EndNode.FirstName == "Elen");
    }

    [Fact]
    public async Task TraversePaths_Where_FiltersByEndAndDepth()
    {
        // Setup:
        // Alice is 25, Bob is 30, Charlie is 40
        // Alice knows Bob (30) and Elen (45)
        // Bob knows Charlie (40)

        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };
        var elen = new Person { FirstName = "Elen", LastName = "White", Age = 45 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(elen, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows within up to two hops who are over 35, using the
        // IGraphPath predicates are applied while the provider still has one row per matched path.
        // This filters both by an end-node property and by path depth on the server.
        var olderFriendPaths = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .TraversePaths<Knows, Person>(minDepth: 1, maxDepth: 2)
            .Where(path => path.Start.Id == alice.Id &&
                ((Person)path.End).Age > 35 &&
                path.Segments.Count >= 1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, olderFriendPaths.Count);
        Assert.Contains(olderFriendPaths, path => ((Person)path.End).FirstName == "Charlie" && path.Segments.Count == 2);
        Assert.Contains(olderFriendPaths, path => ((Person)path.End).FirstName == "Elen" && path.Segments.Count == 1);
    }

    #endregion

    #region Multiple Relationship Types Tests

    [Fact]
    public async Task CanTraverseMultipleRelationshipTypes()
    {
        // Setup: Alice knows Bob and lives at address1
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var address1 = new Address { Street = "123 Main St", City = "Seattle" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(address1, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new LivesAt { StartNodeId = alice.Id, EndNodeId = address1.Id, MovedInDate = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Test traversing Knows relationships
        var knownPeople = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(knownPeople);
        Assert.Equal("Bob", knownPeople[0].FirstName);

        // Test traversing LivesAt relationships
        var addresses = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<LivesAt, Address>()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(addresses);
        Assert.Equal("123 Main St", addresses[0].Street);
    }

    #endregion

    #region Relationship Query Tests

    [Fact]
    public async Task CanQueryRelationshipsDirectly()
    {
        // Setup: Alice knows Bob and Charlie with different relationship properties
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(oldKnows, null, TestContext.Current.CancellationToken);

        // Act: Get the relationships themselves
        var relationships = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Select(p => p.Relationship)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, relationships.Count);
        Assert.Contains(relationships, r => r.Since > DateTime.UtcNow.AddYears(-1)); // Recent
        Assert.Contains(relationships, r => r.Since < DateTime.UtcNow.AddYears(-1)); // Old
    }

    [Fact]
    public async Task CanFilterRelationshipsAndQueryThem()
    {
        // Setup: Alice knows Bob (recent) and Charlie (old)
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(oldKnows, null, TestContext.Current.CancellationToken);

        // Act: Get only recent relationships
        var recentRelationships = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since > DateTime.UtcNow.AddYears(-1))
            .Select(p => p.Relationship)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(recentRelationships);

        Assert.True(recentRelationships[0].Since > DateTime.UtcNow.AddYears(-1));
    }

    #endregion

    #region Path Query Tests

    [Fact]
    public async Task CanQueryPathsWithSourceAndTarget()
    {
        // Setup: Alice knows Bob
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };
        await Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        // Act: Get paths from Alice to other people
        var paths = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(paths);
        var path = paths[0];
        Assert.Equal("Alice", path.StartNode.FirstName);
        Assert.Equal("Bob", path.EndNode.FirstName);
        Assert.Equal(alice.Id, path.Relationship.StartNodeId);
        Assert.Equal(bob.Id, path.Relationship.EndNodeId);
    }

    [Fact]
    public async Task CanQueryMultiHopPaths()
    {
        // Setup: Alice -> Bob -> Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Get 2-hop paths from Alice
        var aliceKnowsTransitively = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, aliceKnowsTransitively.Count); // Alice->Bob and Alice->Bob->Charlie
        Assert.Contains(aliceKnowsTransitively, p => p.FirstName == "Bob");
        Assert.Contains(aliceKnowsTransitively, p => p.FirstName == "Charlie");
    }

    #endregion

    #region Complex Traversal Tests

    [Fact]
    public async Task CanCombineTraversalWithLinqOperations()
    {
        // Setup: Multiple people with various connections
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 35 };
        var diana = new Person { FirstName = "Diana", LastName = "White", Age = 28 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(diana, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = diana.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice knows who are over 30, ordered by age
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .Where(p => p.Age > 30)
            .OrderBy(p => p.Age)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal("Charlie", results[0].FirstName);
        Assert.Equal(35, results[0].Age);
    }

    [Fact]
    public async Task CanTraverseAndAggregateResults()
    {
        // Setup: Alice knows multiple people
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 35 };
        var diana = new Person { FirstName = "Diana", LastName = "White", Age = 28 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(diana, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = diana.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Count people Alice knows
        var friendCount = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .CountAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, friendCount);

        // Act: Get average age of people Alice knows
        var averageAge = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .AverageAsync(p => p.Age, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(31, averageAge); // (30 + 35 + 28) / 3 = 31
    }

    [Fact]
    public async Task CanUseTraversalWithStringAndDateTimePredicates()
    {
        // Setup: Create a social network
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25, Bio = "Engineer" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30, Bio = "Developer" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 35, Bio = "Manager" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddMonths(-6) }, null, TestContext.Current.CancellationToken);

        // Act: Find people Alice has known for over a year who are developers or engineers and a custom project
        var techFriendsBios = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since < DateTime.UtcNow.AddYears(-1))
            .Where(p => p.EndNode.Bio.Contains("Developer") || p.EndNode.Bio.Contains("Engineer"))
            .Select(p => new { Name = p.EndNode.FirstName, Bio = p.EndNode.Bio })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(techFriendsBios);
        Assert.Equal("Bob", techFriendsBios[0].Name);
        Assert.Equal("Developer", techFriendsBios[0].Bio);
    }

    [Fact]
    public async Task CanTraverseMultipleRelationshipsFromTheSameNode()
    {
        var alice = new Person { FirstName = "Alice", Age = 30 };
        var bob = new Person { FirstName = "Bob", Age = 28 };
        var charlie = new Person { FirstName = "Charlie", Age = 35 };
        var diana = new Person { FirstName = "Diana", Age = 32 };
        var eve = new Person { FirstName = "Eve", Age = 29 };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(diana, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(eve, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows(alice.Id, bob.Id) { Since = DateTime.UtcNow.AddYears(-5) }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice.Id, charlie.Id) { Since = DateTime.UtcNow.AddYears(-3) }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(bob.Id, diana.Id) { Since = DateTime.UtcNow.AddYears(-2) }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(charlie.Id, eve.Id) { Since = DateTime.UtcNow.AddYears(-1) }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(diana.Id, eve.Id) { Since = DateTime.UtcNow.AddMonths(-6) }, null, TestContext.Current.CancellationToken);

        var aliceConnections = await Graph.Nodes<Person>()
            .Where(p => p.Id == alice.Id)
            .Traverse<Knows, Person>()
            .Select(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: Alice should have connections to Bob and Charlie
        Assert.Equal(2, aliceConnections.Count);
        Assert.Contains(aliceConnections, id => id == bob.Id);
        Assert.Contains(aliceConnections, id => id == charlie.Id);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task TraversalWithNoResultsReturnsEmptyCollection()
    {
        // Setup: Alice with no connections
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);

        // Act: Try to traverse from Alice
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task TraversalFromNonExistentNodeReturnsEmpty()
    {
        // Setup: Alice with no connections
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);

        // Act: Try to traverse from a non-existent person
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "NonExistent")
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Performance and Edge Case Tests

    [Fact]
    public async Task CanHandleLargeTraversalResults()
    {
        // Setup: Create Alice connected to many people
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);

        const int friendCount = 50; // Reduced for test performance

        for (int i = 0; i < friendCount; i++)
        {
            var friend = new Person { FirstName = $"Friend{i}", LastName = "Test" };
            await Graph.CreateNodeAsync(friend, null, TestContext.Current.CancellationToken);
            await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = friend.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        }

        // Act: Traverse to all friends
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(friendCount, results.Count);
        Assert.All(results, r => Assert.StartsWith("Friend", r.FirstName));
    }

    [Fact]
    public async Task CanHandleCircularReferences()
    {
        // Setup: Alice -> Bob -> Charlie -> Alice (circular)
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: Traverse with max depth 3 to potentially encounter the cycle
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(3)
            .Distinct() // Use Distinct to avoid duplicates from cycles
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: Should handle the circular reference gracefully
        Assert.True(results.Count >= 2); // At least Bob and Charlie
        Assert.Contains(results, p => p.FirstName == "Bob");
        Assert.Contains(results, p => p.FirstName == "Charlie");
    }

    #endregion

    #region Multiple PathSegments with Incoming Direction Tests

    [Fact]
    public async Task CanUseMultiplePathSegmentsWithIncomingDirection()
    {
        var alice = new User { Name = "Alice", Email = "alice@example.com", GoogleId = "alice-google-id" };
        var memory = new MemoryWithoutSourceProperty
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Location = new Point { Height = 0, Latitude = 47.6062, Longitude = -122.3321 },
            Deleted = false,
            Text = "memory"
        };

        var memorySource = new MemorySourceNode
        {
            Name = "Source1",
            Description = "Test Source",
            Version = "1.0",
            Device = "Device1",
        };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memorySource, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new UserMemory { StartNodeId = alice.Id, EndNodeId = memory.Id }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new MemoryToMemorySourceNode { StartNodeId = memory.Id, EndNodeId = memorySource.Id }, null, TestContext.Current.CancellationToken);

        // Act: Use multiple PathSegments with incoming direction
        // This should generate: (tgt_2:MemorySourceNode)<-[r_1:MemoryToMemorySourceNode]-(src:MemoryWithoutSourceProperty)<-[r:MEMORY]-(tgt:User)
        var results = await Graph.Nodes<MemoryWithoutSourceProperty>()
            .PathSegments<MemoryWithoutSourceProperty, UserMemory, User>(GraphTraversalDirection.Incoming)
            .Where(ps => ps.EndNode.Name == "Alice")
            .Select(ps => ps.StartNode)
            .PathSegments<MemoryWithoutSourceProperty, MemoryToMemorySourceNode, MemorySourceNode>()
            .Select(ps => new { Memory = ps.StartNode, MemorySource = ps.EndNode })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal("memory", results[0].Memory.Text);
        Assert.Equal("Source1", results[0].MemorySource.Name);
    }

    #endregion

    #region Depth/Direction Options and IGraphPath Round-Trip Tests (issue #94)

    [Fact]
    public async Task Traverse_OptionsLambda_AppliesDepthAndDirection()
    {
        // Setup: Alice knows Bob and Charlie; Bob knows Dave (2 hops from Alice)
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };
        var dave = new Person { FirstName = "Dave", LastName = "White" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = dave.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        // Act: depth 1..2 via the options-lambda overload should reach Dave (2 hops) but not
        // introduce anyone unreachable within 2 hops.
        var reachable = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Knows, Person>(o => o.Depth(1, 2))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, reachable.Count);
        Assert.Contains(reachable, p => p.FirstName == "Bob");
        Assert.Contains(reachable, p => p.FirstName == "Charlie");
        Assert.Contains(reachable, p => p.FirstName == "Dave");

        // Act: the options-lambda direction, applied to a 1-hop-only depth, must flip to incoming -
        // from Bob's perspective, only Alice is reachable via Incoming (Alice -> Bob).
        var reverseFromBob = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Bob")
            .Traverse<Knows, Person>(o => o.Depth(1).Direction(GraphTraversalDirection.Incoming))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(reverseFromBob);
        Assert.Equal("Alice", reverseFromBob[0].FirstName);
    }

    [Fact]
    public async Task TraversePaths_OptionsLambda_AppliesDepthAndDirection()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        var paths = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .TraversePaths<Knows, Person>(o => o.Depth(1, 2))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, p => ((Person)p.End).FirstName == "Bob" && p.Segments.Count == 1);
        Assert.Contains(paths, p => ((Person)p.End).FirstName == "Charlie" && p.Segments.Count == 2);
    }

    [Fact]
    public async Task TraversePaths_IGraphPath_RoundTripsOrderedSegmentsAndStartEndConsistency()
    {
        // Setup a 3-hop chain: Alice -> Bob -> Charlie -> Dave.
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };
        var dave = new Person { FirstName = "Dave", LastName = "White" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = dave.Id, Since = DateTime.UtcNow }, null, TestContext.Current.CancellationToken);

        var paths = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .TraversePaths<Knows, Person>(minDepth: 3, maxDepth: 3)
            .ToListAsync(TestContext.Current.CancellationToken);

        var path = Assert.Single(paths);

        // IGraphPath contract (issue #94 scope item 3): Start/End must be consistent with the
        // path's own ordered Segments - Start is the first segment's StartNode, End is the last
        // segment's EndNode, and consecutive segments chain (each segment's EndNode is the next
        // segment's StartNode).
        Assert.Equal(3, path.Segments.Count);
        Assert.Equal("Alice", ((Person)path.Start).FirstName);
        Assert.Equal("Dave", ((Person)path.End).FirstName);

        Assert.Equal(path.Start.Id, path.Segments[0].StartNode.Id);
        Assert.Equal(path.End.Id, path.Segments[^1].EndNode.Id);

        for (var i = 0; i < path.Segments.Count - 1; i++)
        {
            Assert.Equal(path.Segments[i].EndNode.Id, path.Segments[i + 1].StartNode.Id);
        }

        Assert.Equal("Bob", ((Person)path.Segments[0].EndNode).FirstName);
        Assert.Equal("Charlie", ((Person)path.Segments[1].EndNode).FirstName);
        Assert.Equal("Dave", ((Person)path.Segments[2].EndNode).FirstName);
    }

    [Fact]
    public async Task TraversePaths_Select_ProjectsStartEndAndDepth()
    {
        var alice = new Person { FirstName = $"SelectStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"SelectMiddle-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"SelectEnd-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);

        var source = Graph.Nodes<Person>().Where(person => person.Id == alice.Id);
        var starts = await source.TraversePaths<Knows, Person>(2, 2)
            .Select(path => path.Start)
            .ToListAsync(TestContext.Current.CancellationToken);
        var ends = await source.TraversePaths<Knows, Person>(2, 2)
            .Select(path => path.End)
            .ToListAsync(TestContext.Current.CancellationToken);
        var depths = await source.TraversePaths<Knows, Person>(2, 2)
            .Select(path => path.Segments.Count)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(alice.Id, Assert.Single(starts).Id);
        Assert.Equal(charlie.Id, Assert.Single(ends).Id);
        Assert.Equal(2, Assert.Single(depths));
    }

    [Fact]
    public async Task TraversePaths_Take_LimitsPathsBeforeDecomposition()
    {
        var alice = new Person { FirstName = $"TakeStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"TakeEndA-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"TakeEndB-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);

        var paths = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(1, 1)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(paths);
        Assert.Single(paths[0].Segments);
    }

    [Fact]
    public async Task TraversePaths_Skip_SkipsPathsBeforeDecomposition()
    {
        var alice = new Person { FirstName = $"SkipStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"SkipEndA-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"SkipEndB-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);

        var paths = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(1, 1)
            .Skip(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(paths);
        Assert.Single(paths[0].Segments);
    }

    [Fact]
    public async Task TraversePaths_Count_CountsPathsAfterPagination()
    {
        var alice = new Person { FirstName = $"CountStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"CountEndA-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"CountEndB-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);

        var query = Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(1, 1);

        Assert.Equal(2, await query.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await query.Take(1).CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TraversePaths_Any_ChecksPathExistence()
    {
        var alice = new Person { FirstName = $"AnyStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"AnyEnd-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);

        var query = Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(1, 1);

        Assert.True(await query.AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await query.Skip(1).AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TraversePaths_ComplexProperties_HydrateLikePathSegments()
    {
        var alice = new PersonWithComplexProperty
        {
            FirstName = $"HydrationStart-{Guid.NewGuid():N}",
            Address = new AddressValue { Street = "1 Path Way", City = "Seattle" },
        };
        var bob = new PersonWithComplexProperty
        {
            FirstName = $"HydrationMiddle-{Guid.NewGuid():N}",
            Address = new AddressValue { Street = "2 Path Way", City = "Portland" },
        };
        var charlie = new PersonWithComplexProperty
        {
            FirstName = $"HydrationEnd-{Guid.NewGuid():N}",
            Address = new AddressValue { Street = "3 Path Way", City = "Vancouver" },
        };
        var firstRelationship = new Knows(alice, bob);
        var secondRelationship = new Knows(bob, charlie);

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(firstRelationship, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(secondRelationship, null, TestContext.Current.CancellationToken);

        var path = Assert.Single(await Graph.Nodes<PersonWithComplexProperty>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, PersonWithComplexProperty>(2, 2)
            .ToListAsync(TestContext.Current.CancellationToken));
        var firstHop = Assert.Single(await Graph.Nodes<PersonWithComplexProperty>()
            .Where(person => person.Id == alice.Id)
            .PathSegments<PersonWithComplexProperty, Knows, PersonWithComplexProperty>()
            .ToListAsync(TestContext.Current.CancellationToken));
        var secondHop = Assert.Single(await Graph.Nodes<PersonWithComplexProperty>()
            .Where(person => person.Id == bob.Id)
            .PathSegments<PersonWithComplexProperty, Knows, PersonWithComplexProperty>()
            .ToListAsync(TestContext.Current.CancellationToken));

        AssertNodeHydration(firstHop.StartNode, (PersonWithComplexProperty)path.Start);
        AssertNodeHydration(firstHop.StartNode, (PersonWithComplexProperty)path.Segments[0].StartNode);
        AssertNodeHydration(firstHop.EndNode, (PersonWithComplexProperty)path.Segments[0].EndNode);
        AssertNodeHydration(secondHop.StartNode, (PersonWithComplexProperty)path.Segments[1].StartNode);
        AssertNodeHydration(secondHop.EndNode, (PersonWithComplexProperty)path.Segments[1].EndNode);
        AssertNodeHydration(secondHop.EndNode, (PersonWithComplexProperty)path.End);
        Assert.Equal(firstHop.Relationship, path.Segments[0].Relationship);
        Assert.Equal(secondHop.Relationship, path.Segments[1].Relationship);

        static void AssertNodeHydration(PersonWithComplexProperty expected, PersonWithComplexProperty actual)
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.FirstName, actual.FirstName);
            Assert.Equal(expected.Address.Street, actual.Address.Street);
            Assert.Equal(expected.Address.City, actual.Address.City);
        }
    }

    [Fact]
    [RequiresCapability(GraphCapability.RelationshipPredicates)]
    public async Task VariableTraversal_RelationshipPredicateFiltersEveryExpandedHop()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var alice = new Person { FirstName = $"RelPredicateStart-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"RelPredicateRecent-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"RelPredicateOldTail-{Guid.NewGuid():N}" };
        var david = new Person { FirstName = $"RelPredicateRecentTail-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(david, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(alice, bob) { Since = cutoff.AddDays(1) },
            null,
            TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(bob, charlie) { Since = cutoff.AddDays(-1) },
            null,
            TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(bob, david) { Since = cutoff.AddDays(1) },
            null,
            TestContext.Current.CancellationToken);

        var results = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options
                .Depth(1, 2)
                .WhereRelationship<Knows>(relationship => relationship.Since >= cutoff))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(results, person => person.Id == bob.Id);
        Assert.Contains(results, person => person.Id == david.Id);
        Assert.DoesNotContain(results, person => person.Id == charlie.Id);
    }

    [Fact]
    [RequiresCapability(GraphCapability.RelationshipPredicates)]
    public async Task WhereHasRelationship_RespectsDirectionPredicateAndSelfRelationships()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var alice = new Person { FirstName = $"ExistenceAlice-{Guid.NewGuid():N}" };
        var bob = new Person { FirstName = $"ExistenceBob-{Guid.NewGuid():N}" };
        var charlie = new Person { FirstName = $"ExistenceCharlie-{Guid.NewGuid():N}" };

        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(alice, bob) { Since = cutoff.AddDays(1) },
            null,
            TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(charlie, alice) { Since = cutoff.AddDays(-1) },
            null,
            TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(bob, bob) { Since = cutoff.AddDays(1) },
            null,
            TestContext.Current.CancellationToken);

        var outgoing = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id || person.Id == bob.Id || person.Id == charlie.Id)
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Outgoing,
                relationship => relationship.Since >= cutoff)
            .ToListAsync(TestContext.Current.CancellationToken);
        var incoming = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id || person.Id == bob.Id || person.Id == charlie.Id)
            .WhereHasRelationship<Person, Knows>(
                GraphTraversalDirection.Incoming,
                relationship => relationship.Since >= cutoff)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { alice.Id, bob.Id }.Order().ToArray(),
            outgoing.Select(person => person.Id).Order().ToArray());
        Assert.Equal([bob.Id], incoming.Select(person => person.Id).Order().ToArray());
    }

    [Fact]
    [RequiresCapability(GraphCapability.ShortestPath)]
    public async Task ShortestPaths_PinSelectionEndpointDirectionNoPathAndSameNodeSemantics()
    {
        var marker = $"Shortest-{Guid.NewGuid():N}";
        var start = new Person { FirstName = $"{marker}-start" };
        var left = new Person { FirstName = $"{marker}-left" };
        var right = new Person { FirstName = $"{marker}-right" };
        var detour = new Person { FirstName = $"{marker}-detour" };
        var end = new Person { FirstName = $"{marker}-end" };
        var missing = new Person { FirstName = $"{marker}-missing" };

        foreach (var person in new[] { start, left, right, detour, end, missing })
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        foreach (var (from, to) in new[]
        {
            (start, left),
            (left, end),
            (start, right),
            (right, end),
            (start, detour),
            (detour, left),
            (end, start),
            (start, start),
        })
        {
            await Graph.CreateRelationshipAsync(
                new Knows(from, to),
                null,
                TestContext.Current.CancellationToken);
        }

        var oneShortest = await Graph.Nodes<Person>()
            .Where(person => person.Id == start.Id)
            .ShortestPath<Knows, Person>(person => person.Id == end.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var allShortest = await Graph.Nodes<Person>()
            .Where(person => person.Id == start.Id)
            .AllShortestPaths<Knows, Person>(person => person.Id == end.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var incoming = await Graph.Nodes<Person>()
            .Where(person => person.Id == end.Id)
            .ShortestPath<Knows, Person>(
                person => person.Id == start.Id,
                GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);
        var noPath = await Graph.Nodes<Person>()
            .Where(person => person.Id == start.Id)
            .ShortestPath<Knows, Person>(person => person.Id == missing.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        var sameNode = await Graph.Nodes<Person>()
            .Where(person => person.Id == start.Id)
            .ShortestPath<Knows, Person>(person => person.Id == start.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(oneShortest);
        Assert.Equal(2, oneShortest[0].Segments.Count);
        Assert.Equal(2, allShortest.Count);
        Assert.All(allShortest, path => Assert.Equal(2, path.Segments.Count));
        Assert.Single(incoming);
        Assert.Equal(2, incoming[0].Segments.Count);
        Assert.Empty(noPath);
        Assert.Empty(sameNode); // shortest-path queries require at least one hop and exclude the source endpoint.
    }

    [Fact]
    [RequiresCapability(GraphCapability.OptionalTraversal)]
    public async Task OptionalTraverse_PreservesUnmatchedRowsAndPinsMatchDirectionAndProjectionSemantics()
    {
        var marker = $"Optional-{Guid.NewGuid():N}";
        var unmatched = new Person { FirstName = $"{marker}-unmatched" };
        var one = new Person { FirstName = $"{marker}-one" };
        var many = new Person { FirstName = $"{marker}-many" };
        var self = new Person { FirstName = $"{marker}-self" };
        var target1 = new Person { FirstName = $"{marker}-target1" };
        var target2 = new Person { FirstName = $"{marker}-target2" };

        foreach (var person in new[] { unmatched, one, many, self, target1, target2 })
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        foreach (var (from, to) in new[] { (one, target1), (many, target1), (many, target2), (self, self) })
        {
            await Graph.CreateRelationshipAsync(
                new Knows(from, to),
                null,
                TestContext.Current.CancellationToken);
        }

        var results = await Graph.Nodes<Person>()
            .Where(person =>
                person.Id == unmatched.Id || person.Id == one.Id || person.Id == many.Id || person.Id == self.Id)
            .OptionalTraverse<Knows, Person>()
            .Take(10)
            .ToListAsync(TestContext.Current.CancellationToken);
        var incoming = await Graph.Nodes<Person>()
            .Where(person => person.Id == target1.Id)
            .OptionalTraverse<Knows, Person>(GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);
        var projected = await Graph.Nodes<Person>()
            .Where(person => person.Id == unmatched.Id)
            .OptionalTraverse<Knows, Person>()
            .Select(result => new
            {
                SourceId = result.Source.Id,
                TargetId = result.Target == null ? null : result.Target.Id,
            })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        Assert.Contains(results, result => result.Source.Id == unmatched.Id && result.Target is null);
        Assert.Contains(results, result => result.Source.Id == one.Id && result.Target?.Id == target1.Id);
        Assert.Equal(2, results.Count(result => result.Source.Id == many.Id && result.Target is not null));
        Assert.Contains(results, result => result.Source.Id == self.Id && result.Target?.Id == self.Id);
        Assert.Equal(
            new[] { one.Id, many.Id }.Order().ToArray(),
            incoming.Select(result => result.Target!.Id).Order().ToArray());
        Assert.Equal(unmatched.Id, Assert.Single(projected).SourceId);
        Assert.Null(projected[0].TargetId);
    }

    [Fact]
    [RequiresCapability(GraphCapability.OptionalTraversal)]
    [RequiresCapability(GraphCapability.LabelFiltering)]
    public async Task OptionalTraverse_SourceLabelFilterEliminatesRowsBeforeTheLeftMatch()
    {
        var marker = $"OptionalFilter-{Guid.NewGuid():N}";
        var manager = new Manager { FirstName = $"{marker}-manager", Department = "Ops" };
        var person = new Person { FirstName = $"{marker}-person" };
        var target = new Person { FirstName = $"{marker}-target" };

        await Graph.CreateNodeAsync(manager, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(person, target),
            null,
            TestContext.Current.CancellationToken);

        var results = await Graph.Nodes<Person>()
            .Where(node => node.Id == manager.Id || node.Id == person.Id)
            .OfLabel(nameof(Manager))
            .OptionalTraverse<Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // The label filter must eliminate the non-manager source entirely - even though it has a
        // matching relationship - instead of degrading into a preserved row with a null target.
        var result = Assert.Single(results);
        Assert.Equal(manager.Id, result.Source.Id);
        Assert.Null(result.Target);
    }

    [Fact]
    [RequiresCapability(GraphCapability.SetOperations)]
    public async Task TypedUnionAndConcat_PinDistinctBagAndScalarProjectionSemantics()
    {
        var marker = $"SetOperation-{Guid.NewGuid():N}";
        var first = new Person { FirstName = $"{marker}-first" };
        var overlap = new Person { FirstName = $"{marker}-overlap" };
        var third = new Person { FirstName = $"{marker}-third" };
        await Graph.CreateNodeAsync(first, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(overlap, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(third, null, TestContext.Current.CancellationToken);

        var left = Graph.Nodes<Person>().Where(person => person.Id == first.Id || person.Id == overlap.Id);
        var right = Graph.Nodes<Person>().Where(person => person.Id == overlap.Id);
        var firstOnly = Graph.Nodes<Person>().Where(person => person.Id == first.Id);
        var overlapOnly = Graph.Nodes<Person>().Where(person => person.Id == overlap.Id);
        var thirdOnly = Graph.Nodes<Person>().Where(person => person.Id == third.Id);

        var union = await left.Union(right).ToListAsync(TestContext.Current.CancellationToken);
        var concat = await left.Concat(right).ToListAsync(TestContext.Current.CancellationToken);
        var scalarUnion = await left.Select(person => person.Id)
            .Union(right.Select(person => person.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        var scalarConcat = await left.Select(person => person.Id)
            .Concat(right.Select(person => person.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        var chainedUnion = await firstOnly
            .Union(overlapOnly)
            .Union(thirdOnly)
            .Union(overlapOnly)
            .ToListAsync(TestContext.Current.CancellationToken);
        var nestedUnion = await firstOnly
            .Union(overlapOnly.Union(thirdOnly.Union(overlapOnly)))
            .ToListAsync(TestContext.Current.CancellationToken);
        var chainedConcat = await firstOnly
            .Concat(overlapOnly)
            .Concat(thirdOnly)
            .Concat(overlapOnly)
            .ToListAsync(TestContext.Current.CancellationToken);
        var nestedConcat = await firstOnly
            .Concat(overlapOnly.Concat(thirdOnly.Concat(overlapOnly)))
            .ToListAsync(TestContext.Current.CancellationToken);
        var scalarChainedUnion = await firstOnly.Select(person => person.Id)
            .Union(overlapOnly.Select(person => person.Id))
            .Union(thirdOnly.Select(person => person.Id))
            .Union(overlapOnly.Select(person => person.Id))
            .ToListAsync(TestContext.Current.CancellationToken);
        var scalarChainedConcat = await firstOnly.Select(person => person.Id)
            .Concat(overlapOnly.Select(person => person.Id))
            .Concat(thirdOnly.Select(person => person.Id))
            .Concat(overlapOnly.Select(person => person.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, union.Count);
        Assert.Equal(3, concat.Count);
        Assert.Equal(2, scalarUnion.Count);
        Assert.Equal(3, scalarConcat.Count);
        Assert.Equal(2, concat.Count(person => person.Id == overlap.Id));
        Assert.Equal(2, scalarConcat.Count(id => id == overlap.Id));
        Assert.Equal(
            nestedUnion.Select(person => person.Id).Order(),
            chainedUnion.Select(person => person.Id).Order());
        Assert.Equal(3, chainedUnion.Count);
        Assert.Equal(
            [first.Id, overlap.Id, third.Id, overlap.Id],
            chainedConcat.Select(person => person.Id));
        Assert.Equal(nestedConcat.Select(person => person.Id), chainedConcat.Select(person => person.Id));
        Assert.Equal(
            new[] { first.Id, overlap.Id, third.Id }.Order(),
            scalarChainedUnion.Order());
        Assert.Equal(
            [first.Id, overlap.Id, third.Id, overlap.Id],
            scalarChainedConcat);
    }

    [Fact]
    [RequiresCapability(GraphCapability.LabelFiltering)]
    public async Task LabelFilters_PinSubtypeDynamicAnyAllEmptyCompositionAndSafetySemantics()
    {
        var marker = $"LabelFilter-{Guid.NewGuid():N}";
        var person = new Person { FirstName = $"{marker}-person" };
        var manager = new Manager { FirstName = $"{marker}-manager", Department = "Ops" };
        var dynamicNode = new DynamicNode(
            labels: [marker, "Active"],
            properties: new Dictionary<string, object?> { ["marker"] = marker });

        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(manager, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(dynamicNode, null, TestContext.Current.CancellationToken);

        var subtype = await Graph.Nodes<Person>()
            .Where(node => node.Id == person.Id || node.Id == manager.Id)
            .OfLabel(nameof(Manager))
            .OrderBy(node => node.Id)
            .Take(5)
            .ToListAsync(TestContext.Current.CancellationToken);
        var any = await Graph.DynamicNodes()
            .Where(node => node.Id == dynamicNode.Id)
            .OfLabels(GraphLabelMatch.Any, "Missing", marker)
            .ToListAsync(TestContext.Current.CancellationToken);
        var all = await Graph.DynamicNodes()
            .Where(node => node.Id == dynamicNode.Id)
            .OfLabels(GraphLabelMatch.All, marker, "Active")
            .ToListAsync(TestContext.Current.CancellationToken);
        var empty = await Graph.DynamicNodes()
            .Where(node => node.Id == dynamicNode.Id)
            .OfLabels(GraphLabelMatch.All)
            .ToListAsync(TestContext.Current.CancellationToken);
        var hostile = await Graph.DynamicNodes()
            .Where(node => node.Id == dynamicNode.Id)
            .OfLabel("Active') OR true //")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(manager.Id, Assert.Single(subtype).Id);
        Assert.Equal(dynamicNode.Id, Assert.Single(any).Id);
        Assert.Equal(dynamicNode.Id, Assert.Single(all).Id);
        Assert.Equal(dynamicNode.Id, Assert.Single(empty).Id);
        Assert.Empty(hostile);
    }

    #endregion

    #region Self-Loop Traversal Tests

    [Fact]
    public async Task SelfLoop_IsTraversedOncePerDirectionShape()
    {
        // A self-loop is one physical relationship. Outgoing and Incoming each match it once, and
        // Both must not emit it twice just because it matches from either end (#380). This mirrors
        // RelationshipCounts_CountAnUndirectedSelfLoopOnce, so traversal and degree agree.
        var alice = new Person { FirstName = "Alice", LastName = "Loop" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows { StartNodeId = alice.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow },
            null,
            TestContext.Current.CancellationToken);

        foreach (var direction in new[]
        {
            GraphTraversalDirection.Outgoing,
            GraphTraversalDirection.Incoming,
            GraphTraversalDirection.Both,
        })
        {
            var results = await Graph.Nodes<Person>()
                .Where(person => person.Id == alice.Id)
                .Traverse<Knows, Person>(options => options.Direction(direction))
                .ToListAsync(TestContext.Current.CancellationToken);

            var reached = Assert.Single(results);
            Assert.Equal(alice.Id, reached.Id);
        }
    }

    [Fact]
    public async Task SelfLoop_ProducesOneSegmentAndOnePath()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Loop" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows { StartNodeId = alice.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow },
            null,
            TestContext.Current.CancellationToken);

        foreach (var direction in new[]
        {
            GraphTraversalDirection.Outgoing,
            GraphTraversalDirection.Incoming,
            GraphTraversalDirection.Both,
        })
        {
            var segments = await Graph.Nodes<Person>()
                .Where(person => person.Id == alice.Id)
                .PathSegments<Person, Knows, Person>(direction)
                .ToListAsync(TestContext.Current.CancellationToken);

            var paths = await Graph.Nodes<Person>()
                .Where(person => person.Id == alice.Id)
                .TraversePaths<Knows, Person>(options => options.Depth(1, 1).Direction(direction))
                .ToListAsync(TestContext.Current.CancellationToken);

            var segment = Assert.Single(segments);
            Assert.Equal(alice.Id, segment.StartNode.Id);
            Assert.Equal(alice.Id, segment.EndNode.Id);

            var path = Assert.Single(paths);
            Assert.Single(path.Segments);
        }
    }

    [Fact]
    public async Task ParallelSelfLoops_PreserveOneResultPerRelationship()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Parallel" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(alice, alice), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(alice, alice), null, TestContext.Current.CancellationToken);

        var traversed = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options.Direction(GraphTraversalDirection.Both))
            .ToListAsync(TestContext.Current.CancellationToken);
        var segmentEnds = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
            .Select(segment => segment.EndNode)
            .ToListAsync(TestContext.Current.CancellationToken);
        var pathEnds = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(options => options.Depth(1, 1).Direction(GraphTraversalDirection.Both))
            .Select(path => path.End)
            .ToListAsync(TestContext.Current.CancellationToken);
        var distinct = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options.Direction(GraphTraversalDirection.Both))
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, traversed.Count);
        Assert.Equal(2, segmentEnds.Count);
        Assert.Equal(2, pathEnds.Count);
        Assert.Single(distinct);
        Assert.All(traversed, reached => Assert.Equal(alice.Id, reached.Id));
        Assert.All(segmentEnds, reached => Assert.Equal(alice.Id, reached.Id));
        Assert.All(pathEnds, reached => Assert.Equal(alice.Id, reached.Id));
    }

    [Fact]
    public async Task OppositeDirectionRelationships_PreserveOneResultPerRelationship()
    {
        var alice = new Person { FirstName = "Alice", LastName = "Opposite" };
        var bob = new Person { FirstName = "Bob", LastName = "Opposite" };
        await Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(
            new Knows(bob, alice), null, TestContext.Current.CancellationToken);

        var traversed = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .Traverse<Knows, Person>(options => options.Direction(GraphTraversalDirection.Both))
            .ToListAsync(TestContext.Current.CancellationToken);
        var segmentEnds = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .PathSegments<Person, Knows, Person>(GraphTraversalDirection.Both)
            .Select(segment => segment.EndNode)
            .ToListAsync(TestContext.Current.CancellationToken);
        var pathEnds = await Graph.Nodes<Person>()
            .Where(person => person.Id == alice.Id)
            .TraversePaths<Knows, Person>(options => options.Depth(1, 1).Direction(GraphTraversalDirection.Both))
            .Select(path => path.End)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, traversed.Count);
        Assert.Equal(2, segmentEnds.Count);
        Assert.Equal(2, pathEnds.Count);
        Assert.All(traversed, reached => Assert.Equal(bob.Id, reached.Id));
        Assert.All(segmentEnds, reached => Assert.Equal(bob.Id, reached.Id));
        Assert.All(pathEnds, reached => Assert.Equal(bob.Id, reached.Id));
    }

    #endregion
}

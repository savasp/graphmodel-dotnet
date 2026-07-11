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
        // PathSegments has no options-lambda direction overload (only Traverse/TraverseRelationships/
        // TraversePaths do); the free-floating Direction() modifier is the only way to express this.
#pragma warning disable CS0618
        var pathSegments = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Incoming)
            .ToListAsync(TestContext.Current.CancellationToken);
#pragma warning restore CS0618

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
    public async Task CanFilterTargetNodesByPropertyWithoutTraverseTwoHops()
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
            .Where(path => ((Person)path.End).Age > 35 && path.Segments.Count >= 1)
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
        // PathSegments has no options-lambda direction overload; Direction() is the only way to
        // express this today.
#pragma warning disable CS0618
        var results = await Graph.Nodes<MemoryWithoutSourceProperty>()
            .PathSegments<MemoryWithoutSourceProperty, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(ps => ps.EndNode.Name == "Alice")
            .Select(ps => ps.StartNode)
            .PathSegments<MemoryWithoutSourceProperty, MemoryToMemorySourceNode, MemorySourceNode>()
            .Select(ps => new { Memory = ps.StartNode, MemorySource = ps.EndNode })
            .ToListAsync(TestContext.Current.CancellationToken);
#pragma warning restore CS0618

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

    #endregion
}

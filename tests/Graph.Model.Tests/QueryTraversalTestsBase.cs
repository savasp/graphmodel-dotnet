// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Tests;

public abstract class QueryTraversalTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    #region Basic Traversal Tests

    [Fact]
    public async Task CanGetPathSegments()
    {
        // Setup: Alice knows Bob and Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) };
        var aliceKnowsCharlie = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-1) };

        await Graph.CreateRelationshipAsync(aliceKnowsBob);
        await Graph.CreateRelationshipAsync(aliceKnowsCharlie);

        // Act: Traverse from Alice to people she knows
        var knownPeople = await Graph.Nodes<Person>()
            .PathSegments<Person, Knows, Person>()
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) };
        var aliceKnowsCharlie = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-1) };

        await Graph.CreateRelationshipAsync(aliceKnowsBob);
        await Graph.CreateRelationshipAsync(aliceKnowsCharlie);

        // Act: Traverse from Alice to people she knows
        var knownPeople = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var aliceKnowsBob = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };
        var bobKnowsCharlie = new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow };

        await Graph.CreateRelationshipAsync(aliceKnowsBob);
        await Graph.CreateRelationshipAsync(bobKnowsCharlie);

        // Act & Assert: Depth 1 - should only get Bob
        var depth1Results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(1)
            .ToListAsync();

        Assert.Single(depth1Results);
        Assert.Equal("Bob", depth1Results[0].FirstName);

        // Act & Assert: Depth 2 - should get Bob and Charlie
        var depth2Results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(2)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(david);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = david.Id, Since = DateTime.UtcNow });

        // Act: Traverse with depth range 2-3 (should get Charlie and David, but not Bob)
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(2, 3)
            .ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.FirstName == "Charlie");
        Assert.Contains(results, p => p.FirstName == "David");
        Assert.DoesNotContain(results, p => p.FirstName == "Bob");
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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse outgoing from Alice (should only get Bob)
        var outgoingResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Outgoing)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse incoming to Alice (should only get Charlie)
        var incomingResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Incoming)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse in both directions from Alice (should get both Bob and Charlie)
        var bothDirectionsResults = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Direction(GraphTraversalDirection.Both)
            .ToListAsync();

        // Assert
        Assert.Equal(2, bothDirectionsResults.Count);
        Assert.Contains(bothDirectionsResults, p => p.FirstName == "Bob");
        Assert.Contains(bothDirectionsResults, p => p.FirstName == "Charlie");
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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows);
        await Graph.CreateRelationshipAsync(oldKnows);

        // Act: Find people Alice has known for less than a year
        var recentFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since > DateTime.UtcNow.AddYears(-1))
            .Select(p => p.EndNode)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(elen);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows directly who are over 50
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Where(p => p.Age > 50)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(elen);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows directly who are over 25
        // This should return Bob (30) and Elen (45)
        // Charlie isn't directly connected to Alice, so he won't be included
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Where(p => p.Age > 25)
            .ToListAsync();

        // Assert
        Assert.Equal(2, olderFriends.Count);
        Assert.Equal("Bob", olderFriends[0].FirstName);
        Assert.Equal("Elen", olderFriends[1].FirstName);
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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(elen);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = elen.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows with two hops who are over 35
        // We should get Charlie (40) through Bob, and Elen directly

        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(2)
            .Where(p => p.Age > 35)
            .ToListAsync();

        Assert.Equal(2, olderFriends.Count);
        Assert.Contains(olderFriends, p => p.FirstName == "Charlie");
        Assert.Contains(olderFriends, p => p.FirstName == "Elen");
    }

    [Fact]
    public async Task CanFilterTargetNodesByPropertyWithoutTraverse()
    {
        // Setup: Alice knows Bob (30) and Charlie (40)
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows who are over 35 with one hop
        var olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(ps => ps.EndNode.Age > 35)
            .ToListAsync();

        // Assert
        Assert.Empty(olderFriends); // No friends over 35 with one hop

        olderFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .WithDepth(2)
            .Where(ps => ps.EndNode.Age > 35)
            .ToListAsync();

        Assert.Empty(olderFriends); // No friends over 35 with two hops
        Assert.Equal(charlie.FirstName, olderFriends[0].EndNode.FirstName); // Charlie is over 35
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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(address1);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new LivesAt { StartNodeId = alice.Id, EndNodeId = address1.Id, MovedInDate = DateTime.UtcNow });

        // Test traversing Knows relationships
        var knownPeople = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .ToListAsync();

        Assert.Single(knownPeople);
        Assert.Equal("Bob", knownPeople[0].FirstName);

        // Test traversing LivesAt relationships
        var addresses = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, LivesAt, Address>()
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows);
        await Graph.CreateRelationshipAsync(oldKnows);

        // Act: Get the relationships themselves
        var relationships = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Select(p => p.Relationship)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        var recentKnows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationshipAsync(recentKnows);
        await Graph.CreateRelationshipAsync(oldKnows);

        // Act: Get only recent relationships
        var recentRelationships = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since > DateTime.UtcNow.AddYears(-1))
            .Select(p => p.Relationship)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);

        var knows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };
        await Graph.CreateRelationshipAsync(knows);

        // Act: Get paths from Alice to other people
        var paths = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });

        // Act: Get 2-hop paths from Alice
        var aliceKnowsTransitively = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(2)
            .ToListAsync();

        foreach (var person in aliceKnowsTransitively)
        {
            Console.WriteLine($"Alice knows {person.FirstName}");
        }

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(diana);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = diana.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows who are over 30, ordered by age
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .Where(p => p.Age > 30)
            .OrderBy(p => p.Age)
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);
        await Graph.CreateNodeAsync(diana);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = diana.Id, Since = DateTime.UtcNow });

        // Act: Count people Alice knows
        var friendCount = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .CountAsync();

        // Assert
        Assert.Equal(3, friendCount);

        // Act: Get average age of people Alice knows
        var averageAge = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .AverageAsync(p => p.Age);

        // Assert
        Assert.Equal(31, averageAge); // (30 + 35 + 28) / 3 = 31
    }

    [Fact]
    public async Task CanUseTraversalWithComplexPredicates()
    {
        // Setup: Create a social network
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25, Bio = "Engineer" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30, Bio = "Developer" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 35, Bio = "Manager" };

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow.AddMonths(-6) });

        // Act: Find people Alice has known for over a year who are developers or engineers
        var techFriends = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .PathSegments<Person, Knows, Person>()
            .Where(k => k.Relationship.Since < DateTime.UtcNow.AddYears(-1))
            .Where(p => p.EndNode.Bio.Contains("Developer") || p.EndNode.Bio.Contains("Engineer"))
            .Select(p => p.EndNode)
            .ToListAsync();

        // Assert
        Assert.Single(techFriends);
        Assert.Equal("Bob", techFriends[0].FirstName);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task TraversalWithNoResultsReturnsEmptyCollection()
    {
        // Setup: Alice with no connections
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        await Graph.CreateNodeAsync(alice);

        // Act: Try to traverse from Alice
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .ToListAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task TraversalFromNonExistentNodeReturnsEmpty()
    {
        // Act: Try to traverse from a non-existent person
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "NonExistent")
            .Traverse<Person, Knows, Person>()
            .ToListAsync();

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
        await Graph.CreateNodeAsync(alice);

        var friends = new List<Person>();
        const int friendCount = 50; // Reduced for test performance

        for (int i = 0; i < friendCount; i++)
        {
            var friend = new Person { FirstName = $"Friend{i}", LastName = "Test" };
            friends.Add(friend);
            await Graph.CreateNodeAsync(friend);
            await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = friend.Id, Since = DateTime.UtcNow });
        }

        // Act: Traverse to all friends
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .ToListAsync();

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

        await Graph.CreateNodeAsync(alice);
        await Graph.CreateNodeAsync(bob);
        await Graph.CreateNodeAsync(charlie);

        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = bob.Id, EndNodeId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationshipAsync(new Knows { StartNodeId = charlie.Id, EndNodeId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse with max depth 3 to potentially encounter the cycle
        var results = await Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows, Person>()
            .WithDepth(3)
            .Distinct() // Use Distinct to avoid duplicates from cycles
            .ToListAsync();

        // Assert: Should handle the circular reference gracefully
        Assert.True(results.Count >= 2); // At least Bob and Charlie
        Assert.Contains(results, p => p.FirstName == "Bob");
        Assert.Contains(results, p => p.FirstName == "Charlie");
    }

    #endregion
}
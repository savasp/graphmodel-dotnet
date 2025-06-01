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
    public async Task CanTraverseToConnectedNodes()
    {
        // Setup: Alice knows Bob and Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        var aliceKnowsBob = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) };
        var aliceKnowsCharlie = new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow.AddYears(-1) };

        await Graph.CreateRelationship(aliceKnowsBob);
        await Graph.CreateRelationship(aliceKnowsCharlie);

        // Act: Traverse from Alice to people she knows
        var knownPeople = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        var aliceKnowsBob = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow };
        var bobKnowsCharlie = new Knows { SourceId = bob.Id, TargetId = charlie.Id, Since = DateTime.UtcNow };

        await Graph.CreateRelationship(aliceKnowsBob);
        await Graph.CreateRelationship(bobKnowsCharlie);

        // Act & Assert: Depth 1 - should only get Bob
        var depth1Results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WithDepth(1)
            .To<Person>()
            .ToList();

        Assert.Single(depth1Results);
        Assert.Equal("Bob", depth1Results[0].FirstName);

        // Act & Assert: Depth 2 - should get Bob and Charlie
        var depth2Results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WithDepth(2)
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);
        await Graph.CreateNode(david);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = bob.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = charlie.Id, TargetId = david.Id, Since = DateTime.UtcNow });

        // Act: Traverse with depth range 2-3 (should get Charlie and David, but not Bob)
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WithDepth(2, 3)
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = charlie.Id, TargetId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse outgoing from Alice (should only get Bob)
        var outgoingResults = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .InDirection(TraversalDirection.Outgoing)
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = charlie.Id, TargetId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse incoming to Alice (should only get Charlie)
        var incomingResults = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .InDirection(TraversalDirection.Incoming)
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = charlie.Id, TargetId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse in both directions from Alice (should get both Bob and Charlie)
        var bothDirectionsResults = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .InDirection(TraversalDirection.Both)
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        var recentKnows = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationship(recentKnows);
        await Graph.CreateRelationship(oldKnows);

        // Act: Find people Alice has known for less than a year
        var recentFriends = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WhereRelationship(k => k.Since > DateTime.UtcNow.AddYears(-1))
            .To<Person>()
            .ToList();

        // Assert
        Assert.Single(recentFriends);
        Assert.Equal("Bob", recentFriends[0].FirstName);
    }

    [Fact]
    public async Task CanFilterTargetNodesByProperty()
    {
        // Setup: Alice knows Bob (30) and Charlie (40)
        var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 30 };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown", Age = 40 };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows who are over 35
        var olderFriends = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>(p => p.Age > 35)
            .ToList();

        // Assert
        Assert.Single(olderFriends);
        Assert.Equal("Charlie", olderFriends[0].FirstName);
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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(address1);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new LivesAt { SourceId = alice.Id, TargetId = address1.Id, MovedInDate = DateTime.UtcNow });

        // Test traversing Knows relationships
        var knownPeople = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .ToList();

        Assert.Single(knownPeople);
        Assert.Equal("Bob", knownPeople[0].FirstName);

        // Test traversing LivesAt relationships
        var addresses = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, LivesAt>()
            .To<Address>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        var recentKnows = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationship(recentKnows);
        await Graph.CreateRelationship(oldKnows);

        // Act: Get the relationships themselves
        var relationships = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .Relationships()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        var recentKnows = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow.AddMonths(-1) };
        var oldKnows = new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow.AddYears(-5) };

        await Graph.CreateRelationship(recentKnows);
        await Graph.CreateRelationship(oldKnows);

        // Act: Get only recent relationships
        var recentRelationships = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WhereRelationship(k => k.Since > DateTime.UtcNow.AddYears(-1))
            .Relationships()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);

        var knows = new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow };
        await Graph.CreateRelationship(knows);

        // Act: Get paths from Alice to other people
        var paths = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .Paths<Person>()
            .ToList();

        // Assert
        Assert.Single(paths);
        var path = paths[0];
        Assert.Equal("Alice", path.Source.FirstName);
        Assert.Equal("Bob", path.Target.FirstName);
        Assert.Equal(alice.Id, path.Relationship.SourceId);
        Assert.Equal(bob.Id, path.Relationship.TargetId);
    }

    [Fact]
    public async Task CanQueryMultiHopPaths()
    {
        // Setup: Alice -> Bob -> Charlie
        var alice = new Person { FirstName = "Alice", LastName = "Smith" };
        var bob = new Person { FirstName = "Bob", LastName = "Jones" };
        var charlie = new Person { FirstName = "Charlie", LastName = "Brown" };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = bob.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });

        // Act: Get 2-hop paths from Alice
        var paths = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WithDepth(2)
            .Paths<Person>()
            .ToList();

        // Assert
        Assert.Equal(2, paths.Count); // Alice->Bob and Alice->Bob->Charlie
        Assert.Contains(paths, p => p.Target.FirstName == "Bob");
        Assert.Contains(paths, p => p.Target.FirstName == "Charlie");
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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);
        await Graph.CreateNode(diana);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = diana.Id, Since = DateTime.UtcNow });

        // Act: Find people Alice knows who are over 30, ordered by age
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .Where(p => p.Age > 30)
            .OrderBy(p => p.Age)
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);
        await Graph.CreateNode(diana);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = diana.Id, Since = DateTime.UtcNow });

        // Act: Count people Alice knows
        var friendCount = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .Count();

        // Assert
        Assert.Equal(3, friendCount);

        // Act: Get average age of people Alice knows
        var averageAge = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .Average(p => p.Age);

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow.AddYears(-2) });
        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = charlie.Id, Since = DateTime.UtcNow.AddMonths(-6) });

        // Act: Find people Alice has known for over a year who are developers or engineers
        var techFriends = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WhereRelationship(k => k.Since < DateTime.UtcNow.AddYears(-1))
            .To<Person>(p => p.Bio.Contains("Developer") || p.Bio.Contains("Engineer"))
            .ToList();

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
        await Graph.CreateNode(alice);

        // Act: Try to traverse from Alice
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void TraversalFromNonExistentNodeReturnsEmpty()
    {
        // Act: Try to traverse from a non-existent person
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "NonExistent")
            .Traverse<Person, Knows>()
            .To<Person>()
            .ToList();

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
        await Graph.CreateNode(alice);

        var friends = new List<Person>();
        const int friendCount = 50; // Reduced for test performance

        for (int i = 0; i < friendCount; i++)
        {
            var friend = new Person { FirstName = $"Friend{i}", LastName = "Test" };
            friends.Add(friend);
            await Graph.CreateNode(friend);
            await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = friend.Id, Since = DateTime.UtcNow });
        }

        // Act: Traverse to all friends
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .To<Person>()
            .ToList();

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

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);

        await Graph.CreateRelationship(new Knows { SourceId = alice.Id, TargetId = bob.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = bob.Id, TargetId = charlie.Id, Since = DateTime.UtcNow });
        await Graph.CreateRelationship(new Knows { SourceId = charlie.Id, TargetId = alice.Id, Since = DateTime.UtcNow });

        // Act: Traverse with max depth 3 to potentially encounter the cycle
        var results = Graph.Nodes<Person>()
            .Where(p => p.FirstName == "Alice")
            .Traverse<Person, Knows>()
            .WithDepth(3)
            .To<Person>()
            .Distinct() // Use Distinct to avoid duplicates from cycles
            .ToList();

        // Assert: Should handle the circular reference gracefully
        Assert.True(results.Count >= 2); // At least Bob and Charlie
        Assert.Contains(results, p => p.FirstName == "Bob");
        Assert.Contains(results, p => p.FirstName == "Charlie");
    }

    #endregion
}
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

    [Fact]
    public async Task QueryNodes_WithDepthZero_ReturnsOnlyNodes()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateRelationship(knows);

        // Act: Query without relationships
        var people = Graph.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 0 })
            .Where(p => p.FirstName == "Alice")
            .ToList();

        // Assert
        Assert.Single(people);
        Assert.Equal("Alice", people[0].FirstName);
        Assert.Empty(people[0].Knows); // No relationships loaded
    }

    [Fact]
    public async Task QueryNodes_WithDepthOne_ReturnsNodesWithRelationships()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };
        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var aliceKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, charlie) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);
        await Graph.CreateRelationship(aliceKnowsBob);
        await Graph.CreateRelationship(aliceKnowsCharlie);

        // Act: Query with relationships
        var people = Graph.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .ToList();

        // Assert
        Assert.Single(people);
        var retrieved = people[0];
        Assert.Equal("Alice", retrieved.FirstName);
        Assert.Equal(2, retrieved.Knows.Count);
        Assert.Contains(retrieved.Knows, k => k.Target!.FirstName == "Bob");
        Assert.Contains(retrieved.Knows, k => k.Target!.FirstName == "Charlie");
    }

    [Fact]
    public async Task QueryNodes_WithFluentApi_WorksCorrectly()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateRelationship(knows);

        // Act: Use fluent API
        var people = Graph.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions().WithRelationships())
            .ToList();

        // Assert
        Assert.Equal(2, people.Count);
        var aliceResult = people.First(p => p.FirstName == "Alice");
        Assert.Single(aliceResult.Knows);
        Assert.Equal("Bob", aliceResult.Knows[0].Target!.FirstName);
    }

    [Fact]
    public async Task QueryRelationships_WithDepthOne_LoadsSourceAndTarget()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateRelationship(knows);

        // Act: Query relationships with nodes
        var relationships = Graph.Relationships<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .ToList();

        // Assert
        Assert.Single(relationships);
        var rel = relationships[0];
        Assert.NotNull(rel.Source);
        Assert.NotNull(rel.Target);
        Assert.Equal("Alice", rel.Source.FirstName);
        Assert.Equal("Bob", rel.Target.FirstName);
    }

    [Fact]
    public async Task QueryNodes_WithRelationshipTypeFilter_LoadsOnlySpecifiedTypes()
    {
        // Setup with multiple relationship types
        var alice = new PersonWithMultipleRelationshipTypes { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var works = new WorksWith(alice, charlie) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateNode(charlie);
        await Graph.CreateRelationship(knows);
        await Graph.CreateRelationship(works);

        // Act: Query with relationship type filter
        var people = Graph.Nodes<PersonWithMultipleRelationshipTypes>(
            new GraphOperationOptions
            {
                TraversalDepth = 1,
                RelationshipTypes = new HashSet<string> { "KNOWS" }
            })
            .Where(p => p.FirstName == "Alice")
            .ToList();

        // Assert
        Assert.Single(people);
        var retrieved = people[0];
        Assert.Single(retrieved.Knows);
        Assert.Empty(retrieved.WorksWith); // Filtered out
    }

    [Fact]
    public async Task QueryNodes_ComplexQuery_WithTraversal()
    {
        // Setup a more complex scenario
        var people = new List<PersonWithNavigationProperty>();
        for (int i = 0; i < 5; i++)
        {
            people.Add(new PersonWithNavigationProperty { FirstName = $"Person{i}", Age = 20 + i });
        }

        // Create nodes
        foreach (var person in people)
        {
            await Graph.CreateNode(person);
        }

        // Create relationships: each person knows the next
        for (int i = 0; i < 4; i++)
        {
            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(people[i], people[i + 1]) { Since = DateTime.UtcNow };
            await Graph.CreateRelationship(knows);
        }

        // Act: Complex query with traversal
        var results = Graph.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 2 })
            .Where(p => p.Age > 21)
            .OrderBy(p => p.FirstName)
            .Take(2)
            .ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Person2", results[0].FirstName);
        Assert.Equal("Person3", results[1].FirstName);

        // Check traversal depth
        Assert.Single(results[0].Knows); // Person2 knows Person3
        Assert.Single(results[0].Knows[0].Target!.Knows); // Person3 knows Person4
    }

    [Fact]
    public async Task QueryNodes_WithProjection_StillAppliesTraversal()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await Graph.CreateNode(alice);
        await Graph.CreateNode(bob);
        await Graph.CreateRelationship(knows);

        // Act: Query with projection
        var results = Graph.Nodes<PersonWithNavigationProperty>(
            new GraphOperationOptions { TraversalDepth = 1 })
            .Where(p => p.FirstName == "Alice")
            .Select(p => new
            {
                Name = p.FirstName,
                FriendCount = p.Knows.Count,
                Friends = p.Knows.Select(k => k.Target!.FirstName)
            })
            .ToList();

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Alice", result.Name);
        Assert.Equal(1, result.FriendCount);
        Assert.Single(result.Friends);
        Assert.Equal("Bob", result.Friends.First());
    }
}
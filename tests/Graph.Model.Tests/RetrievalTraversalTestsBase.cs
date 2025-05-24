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

public abstract class RetrievalTraversalTestsBase
{
    private IGraph provider;

    protected RetrievalTraversalTestsBase(IGraph provider)
    {
        this.provider = provider;
    }

    [Fact]
    public async Task GetNode_WithDepthZero_ReturnsOnlyNode()
    {
        // Setup: Create a graph
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateRelationship(knows);

        // Act: Get node without relationships
        var retrieved = await provider.GetNode<PersonWithNavigationProperty>(alice.Id,
            new GraphOperationOptions { TraversalDepth = 0 });

        // Assert
        Assert.Equal("Alice", retrieved.FirstName);
        Assert.Empty(retrieved.Knows); // No relationships loaded
    }

    [Fact]
    public async Task GetNode_WithDepthOne_ReturnsNodeWithRelationships()
    {
        // Setup: Create a graph
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };
        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var aliceKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, charlie) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateNode(charlie);
        await provider.CreateRelationship(aliceKnowsBob);
        await provider.CreateRelationship(aliceKnowsCharlie);

        // Act: Get node with immediate relationships
        var retrieved = await provider.GetNode<PersonWithNavigationProperty>(alice.Id,
            new GraphOperationOptions { TraversalDepth = 1 });

        // Assert
        Assert.Equal("Alice", retrieved.FirstName);
        Assert.Equal(2, retrieved.Knows.Count);
        Assert.Contains(retrieved.Knows, k => k.Target!.FirstName == "Bob");
        Assert.Contains(retrieved.Knows, k => k.Target!.FirstName == "Charlie");
    }

    [Fact]
    public async Task GetNode_WithFullDepth_ReturnsEntireConnectedGraph()
    {
        // Setup: Create a chain: Alice -> Bob -> Charlie
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };
        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var bobKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(bob, charlie) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateNode(charlie);
        await provider.CreateRelationship(aliceKnowsBob);
        await provider.CreateRelationship(bobKnowsCharlie);

        // Act: Get node with full depth
        var retrieved = await provider.GetNode<PersonWithNavigationProperty>(alice.Id,
            new GraphOperationOptions { TraversalDepth = -1 });

        // Assert
        Assert.Equal("Alice", retrieved.FirstName);
        Assert.Single(retrieved.Knows);

        var retrievedBob = retrieved.Knows[0].Target;
        Assert.Equal("Bob", retrievedBob!.FirstName);
        Assert.Single(retrievedBob.Knows);

        var retrievedCharlie = retrievedBob.Knows[0].Target;
        Assert.Equal("Charlie", retrievedCharlie!.FirstName);
    }

    [Fact]
    public async Task GetRelationship_WithDepthZero_ReturnsOnlyRelationship()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateRelationship(knows);

        // Act: Get relationship without loading nodes
        var retrieved = await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id,
            new GraphOperationOptions { TraversalDepth = 0 });

        // Assert
        Assert.Equal(alice.Id, retrieved.SourceId);
        Assert.Equal(bob.Id, retrieved.TargetId);
        Assert.Null(retrieved.Source); // Nodes not loaded
        Assert.Null(retrieved.Target);
    }

    [Fact]
    public async Task GetRelationship_WithDepthOne_ReturnsRelationshipWithNodes()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateRelationship(knows);

        // Act: Get relationship with nodes
        var retrieved = await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id,
            new GraphOperationOptions { TraversalDepth = 1 });

        // Assert
        Assert.NotNull(retrieved.Source);
        Assert.NotNull(retrieved.Target);
        Assert.Equal("Alice", retrieved.Source.FirstName);
        Assert.Equal("Bob", retrieved.Target.FirstName);
        Assert.Empty(retrieved.Source.Knows); // No deeper traversal
        Assert.Empty(retrieved.Target.Knows);
    }

    [Fact]
    public async Task GetNodes_WithRelationshipTypeFilter_LoadsOnlySpecifiedTypes()
    {
        // Setup with multiple relationship types
        var alice = new PersonWithMultipleRelationshipTypes { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var works = new WorksWith(alice, charlie) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateNode(charlie);
        await provider.CreateRelationship(knows);
        await provider.CreateRelationship(works);

        // Act: Get node with only KNOWS relationships
        var retrieved = await provider.GetNode<PersonWithMultipleRelationshipTypes>(alice.Id,
            new GraphOperationOptions
            {
                TraversalDepth = 1,
                RelationshipTypes = new HashSet<string> { "KNOWS" }
            });

        // Assert
        Assert.Single(retrieved.Knows);
        Assert.Empty(retrieved.WorksWith); // Filtered out
    }

    [Fact]
    public async Task GetNode_WithFluentApi_WorksCorrectly()
    {
        // Setup
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateRelationship(knows);

        // Act: Use fluent API
        var retrieved = await provider.GetNode<PersonWithNavigationProperty>(alice.Id,
            new GraphOperationOptions().WithRelationships());

        // Assert
        Assert.Equal("Alice", retrieved.FirstName);
        Assert.Single(retrieved.Knows);
        Assert.Equal("Bob", retrieved.Knows[0].Target!.FirstName);
    }

    [Fact]
    public async Task GetNodes_Batch_WithTraversal_LoadsAllGraphs()
    {
        // Setup multiple disconnected graphs
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };
        var david = new PersonWithNavigationProperty { FirstName = "David" };

        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        var charlieKnowsDavid = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(charlie, david) { Since = DateTime.UtcNow };

        await provider.CreateNode(alice);
        await provider.CreateNode(bob);
        await provider.CreateNode(charlie);
        await provider.CreateNode(david);
        await provider.CreateRelationship(aliceKnowsBob);
        await provider.CreateRelationship(charlieKnowsDavid);

        // Act: Get multiple nodes with relationships
        var nodes = await provider.GetNodes<PersonWithNavigationProperty>(
            [alice.Id, charlie.Id],
            new GraphOperationOptions { TraversalDepth = 1 });

        // Assert
        var nodeList = nodes.ToList();
        Assert.Equal(2, nodeList.Count);

        var retrievedAlice = nodeList.First(n => n.FirstName == "Alice");
        Assert.Single(retrievedAlice.Knows);
        Assert.Equal("Bob", retrievedAlice.Knows[0].Target!.FirstName);

        var retrievedCharlie = nodeList.First(n => n.FirstName == "Charlie");
        Assert.Single(retrievedCharlie.Knows);
        Assert.Equal("David", retrievedCharlie.Knows[0].Target!.FirstName);
    }
}
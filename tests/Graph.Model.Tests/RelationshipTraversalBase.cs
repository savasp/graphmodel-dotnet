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

public abstract class RelationshipTraversalTestsBase
{
    private IGraph provider;

    protected RelationshipTraversalTestsBase(IGraph provider)
    {
        this.provider = provider;
    }

    [Fact]
    public async Task CreateRelationship_WithCreateMissingNodes_CreatesSourceAndTarget()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        // Create relationship with missing nodes
        await provider.CreateRelationship(knows, new GraphOperationOptions
        {
            CreateMissingNodes = true
        });

        // All should exist
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(alice.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(bob.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id));
    }

    [Fact]
    public async Task CreateRelationship_WithoutCreateMissingNodes_FailsIfNodesNotExist()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        // Should fail because nodes don't exist
        await Assert.ThrowsAsync<GraphException>(() =>
            provider.CreateRelationship(knows, new GraphOperationOptions
            {
                CreateMissingNodes = false
            }));
    }

    [Fact]
    public async Task CreateRelationship_WithDeepTraversal_CreatesConnectedGraph()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

        // Bob knows Charlie
        var bobKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(bob, charlie) { Since = DateTime.UtcNow };
        bob.Knows.Add(bobKnowsCharlie);

        // Create Alice->Bob relationship with deep traversal
        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateRelationship(aliceKnowsBob, new GraphOperationOptions
        {
            TraversalDepth = 2,
            CreateMissingNodes = true
        });

        // All nodes and relationships should exist
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(alice.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(bob.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(charlie.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(aliceKnowsBob.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(bobKnowsCharlie.Id));
    }

    [Fact]
    public async Task UpdateRelationship_WithUpdateExistingNodes_UpdatesConnectedNodes()
    {
        // Setup initial graph
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        await provider.CreateNode(alice);
        await provider.CreateNode(bob);

        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        await provider.CreateRelationship(knows);

        // Update relationship and connected nodes
        knows.Since = DateTime.UtcNow.AddDays(-365);
        alice.FirstName = "Alice Updated";
        bob.FirstName = "Bob Updated";
        knows.Source = alice;
        knows.Target = bob;

        await provider.UpdateRelationship(knows, new GraphOperationOptions
        {
            TraversalDepth = 1,
            UpdateExistingNodes = true
        });

        // Relationship and nodes should be updated
        var fetchedRel = await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id);
        Assert.True((DateTime.UtcNow - fetchedRel.Since).TotalDays > 360);

        var fetchedAlice = await provider.GetNode<PersonWithNavigationProperty>(alice.Id);
        Assert.Equal("Alice Updated", fetchedAlice.FirstName);

        var fetchedBob = await provider.GetNode<PersonWithNavigationProperty>(bob.Id);
        Assert.Equal("Bob Updated", fetchedBob.FirstName);
    }

    [Fact]
    public async Task UpdateRelationship_WithoutUpdateExistingNodes_OnlyUpdatesRelationship()
    {
        // Setup initial graph
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        await provider.CreateNode(alice);
        await provider.CreateNode(bob);

        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        await provider.CreateRelationship(knows);

        // Update relationship but not nodes
        knows.Since = DateTime.UtcNow.AddDays(-365);
        alice.FirstName = "Alice Updated";
        bob.FirstName = "Bob Updated";
        knows.Source = alice;
        knows.Target = bob;

        await provider.UpdateRelationship(knows, new GraphOperationOptions
        {
            TraversalDepth = 1,
            UpdateExistingNodes = false
        });

        // Only relationship should be updated
        var fetchedRel = await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id);
        Assert.True((DateTime.UtcNow - fetchedRel.Since).TotalDays > 360);

        var fetchedAlice = await provider.GetNode<PersonWithNavigationProperty>(alice.Id);
        Assert.Equal("Alice", fetchedAlice.FirstName); // Not updated

        var fetchedBob = await provider.GetNode<PersonWithNavigationProperty>(bob.Id);
        Assert.Equal("Bob", fetchedBob.FirstName); // Not updated
    }

    [Fact]
    public async Task CreateRelationship_WithFluentApi_WorksCorrectly()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

        // Bob has relationship to Charlie
        var bobKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(bob, charlie) { Since = DateTime.UtcNow };
        bob.Knows.Add(bobKnowsCharlie);

        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateRelationship(aliceKnowsBob,
            new GraphOperationOptions()
                .WithRelationships()
                .WithCreateMissingNodes());

        // Alice, Bob, and relationship should exist
        // Charlie should NOT exist (depth = 1 only)
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(alice.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(bob.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(aliceKnowsBob.Id));
        await Assert.ThrowsAsync<GraphException>(() => provider.GetNode<PersonWithNavigationProperty>(charlie.Id));
    }

    [Fact]
    public async Task CreateRelationship_WithFullDepth_CreatesEntireSubgraph()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
        var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };
        var david = new PersonWithNavigationProperty { FirstName = "David" };

        // Create a chain: Alice -> Bob -> Charlie -> David
        var charlieKnowsDavid = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(charlie, david) { Since = DateTime.UtcNow };
        charlie.Knows.Add(charlieKnowsDavid);

        var bobKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(bob, charlie) { Since = DateTime.UtcNow };
        bob.Knows.Add(bobKnowsCharlie);

        var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };

        await provider.CreateRelationship(aliceKnowsBob,
            new GraphOperationOptions()
                .WithFullGraph()
                .WithCreateMissingNodes());

        // Everything should exist
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(alice.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(bob.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(charlie.Id));
        Assert.NotNull(await provider.GetNode<PersonWithNavigationProperty>(david.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(aliceKnowsBob.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(bobKnowsCharlie.Id));
        Assert.NotNull(await provider.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(charlieKnowsDavid.Id));
    }
}
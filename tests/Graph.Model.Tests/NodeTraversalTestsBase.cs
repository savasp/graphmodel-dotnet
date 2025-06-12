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

public abstract class NodeTraversalTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }
    /*
        [Fact]
        public async Task CreateNode_WithDepthZero_CreatesOnlyNode()
        {
            var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            alice.Knows.Add(knows);

            await Graph.CreateNode(alice, new GraphOperationOptions { TraversalDepth = 0 });

            // Alice should exist
            var fetchedAlice = await Graph.GetNode<PersonWithNavigationProperty>(alice.Id);
            Assert.Equal("Alice", fetchedAlice.FirstName);

            // Bob should not exist
            await Assert.ThrowsAsync<GraphException>(() => Graph.GetNode<PersonWithNavigationProperty>(bob.Id));

            // Relationship should not exist
            await Assert.ThrowsAsync<GraphException>(() => Graph.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id));
        }

        [Fact]
        public async Task CreateNode_WithDepthOne_CreatesNodeAndRelationships()
        {
            var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            alice.Knows.Add(knows);

            await Graph.CreateNode(alice, new GraphOperationOptions
            {
                TraversalDepth = 1,
                CreateMissingNodes = true
            });

            // All should exist
            var fetchedAlice = await Graph.GetNode<PersonWithNavigationProperty>(alice.Id);
            Assert.Equal("Alice", fetchedAlice.FirstName);

            var fetchedBob = await Graph.GetNode<PersonWithNavigationProperty>(bob.Id);
            Assert.Equal("Bob", fetchedBob.FirstName);

            var fetchedKnows = await Graph.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id);
            Assert.Equal(alice.Id, fetchedKnows.StartNodeId);
            Assert.Equal(bob.Id, fetchedKnows.EndNodeId);
        }

        [Fact]
        public async Task CreateNode_WithFullDepth_CreatesEntireGraph()
        {
            var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

            var aliceKnowsBob = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            alice.Knows.Add(aliceKnowsBob);

            var bobKnowsCharlie = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(bob, charlie) { Since = DateTime.UtcNow };
            bob.Knows.Add(bobKnowsCharlie);

            await Graph.CreateNode(alice, new GraphOperationOptions
            {
                TraversalDepth = -1,
                CreateMissingNodes = true
            });

            // All nodes should exist
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(alice.Id));
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(bob.Id));
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(charlie.Id));

            // All relationships should exist
            Assert.NotNull(await Graph.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(aliceKnowsBob.Id));
            Assert.NotNull(await Graph.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(bobKnowsCharlie.Id));
        }

        [Fact]
        public async Task UpdateNode_WithOptions_UpdatesRelatedNodes()
        {
            // Setup initial graph
            var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            await Graph.CreateNode(alice);
            await Graph.CreateNode(bob);

            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            await Graph.CreateRelationship(knows);

            // Update Alice and Bob through traversal
            alice.FirstName = "Alice Updated";
            bob.FirstName = "Bob Updated";
            alice.Knows.Add(knows);
            knows.Target = bob;

            await Graph.UpdateNode(alice, new GraphOperationOptions
            {
                TraversalDepth = 1,
                UpdateExistingNodes = true
            });

            // Both should be updated
            var fetchedAlice = await Graph.GetNode<PersonWithNavigationProperty>(alice.Id);
            Assert.Equal("Alice Updated", fetchedAlice.FirstName);

            var fetchedBob = await Graph.GetNode<PersonWithNavigationProperty>(bob.Id);
            Assert.Equal("Bob Updated", fetchedBob.FirstName);
        }

        [Fact]
        public async Task CreateNode_WithRelationshipTypeFilter_OnlyCreatesSpecifiedTypes()
        {
            // Assuming we have different relationship types
            var alice = new PersonWithMultipleRelationshipTypes { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            var charlie = new PersonWithNavigationProperty { FirstName = "Charlie" };

            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            var works = new WorksWith(alice, charlie) { Since = DateTime.UtcNow };

            alice.Knows.Add(knows);
            alice.WorksWith.Add(works);

            await Graph.CreateNode(alice, new GraphOperationOptions
            {
                TraversalDepth = 1,
                CreateMissingNodes = true,
                RelationshipTypes = new HashSet<string> { "KNOWS" }
            });

            // Bob should exist (KNOWS relationship)
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(bob.Id));

            // Charlie should not exist (WORKS_WITH relationship filtered out)
            await Assert.ThrowsAsync<GraphException>(() => Graph.GetNode<PersonWithNavigationProperty>(charlie.Id));
        }

        [Fact]
        public async Task CreateNode_WithFluentApi_WorksCorrectly()
        {
            var alice = new PersonWithNavigationProperty { FirstName = "Alice" };
            var bob = new PersonWithNavigationProperty { FirstName = "Bob" };
            var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
            alice.Knows.Add(knows);

            await Graph.CreateNode(alice,
                new GraphOperationOptions()
                    .WithRelationships()
                    .WithCreateMissingNodes());

            // Both should exist
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(alice.Id));
            Assert.NotNull(await Graph.GetNode<PersonWithNavigationProperty>(bob.Id));
            Assert.NotNull(await Graph.GetRelationship<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>>(knows.Id));
        }
    }

    // Additional test entities
    public class PersonWithMultipleRelationshipTypes : PersonWithNavigationProperty
    {
        public List<WorksWith> WorksWith { get; set; } = new();
    }

    [Relationship("WORKS_WITH")]
    public class WorksWith : Relationship<PersonWithMultipleRelationshipTypes, PersonWithNavigationProperty>
    {
        public DateTime Since { get; set; }

        public WorksWith() { }

        public WorksWith(PersonWithMultipleRelationshipTypes source, PersonWithNavigationProperty target)
        {
            Source = source;
            Target = target;
            StartNodeId = source.Id;
            EndNodeId = target.Id;
        }
    */
}
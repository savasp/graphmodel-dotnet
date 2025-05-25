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

public abstract class BasicTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

    [Fact]
    public async Task CanCreateAndGetNodeWithPrimitiveProperties()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNode(person);
        var fetched = await this.Graph.GetNode<Person>(person.Id);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperties()
    {
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNode(person);
        var fetched = await this.Graph.GetNode<PersonWithComplexProperty>(person.Id);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CannotCreateRelationshipWithComplexProperties()
    {
        var p1 = new PersonWithComplexProperty { FirstName = "A", Address = new Address { Street = "123 Main St", City = "Somewhere" } };
        var p2 = new PersonWithComplexProperty { FirstName = "B" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);

        var address = new Address { Street = "123 Main St", City = "Somewhere" };
        var knows = new KnowsWithComplexProperty(p1, p2) { MetAt = address };

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateRelationship(knows));
    }

    [Fact]
    public async Task CanCreateAndGetRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };

        await this.Graph.CreateRelationship(knows);

        var fetched = await this.Graph.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(p1.Id, fetched.SourceId);
        Assert.Equal(p2.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanCreateRelationshipAndAddNodesAtTheSameTime()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };

        // This should add the nodes and create the relationship
        await this.Graph.CreateRelationship(knows, new GraphOperationOptions { CreateMissingNodes = true });

        var fetched = await this.Graph.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(p1.Id, fetched.SourceId);
        Assert.Equal(p2.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanUpdateNode()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNode(person);
        person.LastName = "Smith";

        await this.Graph.UpdateNode(person);

        var updated = await this.Graph.GetNode<Person>(person.Id);

        Assert.Equal("Smith", updated.LastName);
    }

    [Fact]
    public async Task CanCreateAndDeleteNode()
    {
        var person = new Person { FirstName = "ToDelete" };
        await this.Graph.CreateNode(person);
        await this.Graph.DeleteNode(person.Id);
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetNode<Person>(person.Id));
    }

    [Fact]
    public async Task CanCreateAndDeleteRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };

        await this.Graph.CreateRelationship(knows);
        await this.Graph.DeleteRelationship(knows.Id);
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetRelationship<Knows<Person, Person>>(knows.Id));
    }

    [Fact]
    public async Task CanCreatePersonWithFriendExample()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        var friend = new Person { FirstName = "Jane", LastName = "Smith" };
        await this.Graph.CreateNode(person);
        await this.Graph.CreateNode(friend);
        var knows = new Knows<Person, Person>(person, friend) { Since = DateTime.Now };
        await this.Graph.CreateRelationship(knows);
        var fetched = await this.Graph.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(person.Id, fetched.SourceId);
        Assert.Equal(friend.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanGetMultipleNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);
        var ids = new[] { p1.Id, p2.Id };
        var fetched = await this.Graph.GetNodes<Person>(ids);
        Assert.Equal(2, ((ICollection<Person>)fetched).Count);
        Assert.Contains(fetched, x => x.Id == p1.Id);
        Assert.Contains(fetched, x => x.Id == p2.Id);
    }

    [Fact]
    public async Task CanGetMultipleRelationships()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        var p3 = new Person { FirstName = "C" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);
        await this.Graph.CreateNode(p3);
        var knows1 = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };
        var knows2 = new Knows<Person, Person>(p2, p3) { Since = DateTime.UtcNow };
        await this.Graph.CreateRelationship(knows1);
        await this.Graph.CreateRelationship(knows2);
        var rels = await this.Graph.GetRelationships<Knows<Person, Person>>([knows1.Id, knows2.Id]);
        Assert.Equal(2, ((ICollection<Knows<Person, Person>>)rels).Count);
        Assert.Contains(rels, r => r.Id == knows1.Id);
        Assert.Contains(rels, r => r.Id == knows2.Id);
    }

    [Fact]
    public async Task CanUpdateRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNode(p1);
        await this.Graph.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };
        await this.Graph.CreateRelationship(knows);
        knows.Since = DateTime.UtcNow.AddYears(-1);
        await this.Graph.UpdateRelationship(knows);
        var updated = await this.Graph.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(knows.Id, updated.Id);
        Assert.Equal(p1.Id, updated.SourceId);
        Assert.Equal(p2.Id, updated.TargetId);
        Assert.Equal(knows.Since, updated.Since);
    }

    [Fact]
    public async Task CanBeginTransactionAndRollback()
    {
        var tx = await this.Graph.BeginTransaction();
        var person = new Person { FirstName = "TxTest" };
        await this.Graph.CreateNode(person, new(), tx);
        await tx.DisposeAsync(); // Rollback
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetNode<Person>(person.Id));
    }

    public class PersonWithCycle : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Foo Foo { get; set; } = new();
    }

    public class Foo
    {
        public Foo? Bar { get; set; } = null;
    }

    [Fact]
    public async Task CannotAddNodeWithCycle()
    {
        var person = new PersonWithCycle { FirstName = "A" };
        person.Foo.Bar = new()
        {
            Bar = person.Foo
        };

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithGenericPropertyOfPrimitive : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CanAddNodeWithGenericPropertyOfPrimitive()
    {
        var person = new PersonWithGenericPropertyOfPrimitive { FirstName = "A", GenericProperty = new List<string> { "B" } };

        await this.Graph.CreateNode(person);
    }

    public class PersonWithGenericDictionaryProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Dictionary<string, Person> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CannotAddNodeWithGenericDictionaryProperty()
    {
        var person = new PersonWithGenericDictionaryProperty { FirstName = "A", GenericProperty = new Dictionary<string, Person>() };

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    [Fact]
    public void CanQueryNodesLinq()
    {
        var queryable = this.Graph.Nodes<Person>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public void CanQueryRelationshipsLinq()
    {
        var queryable = this.Graph.Relationships<Knows<Person, Person>>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public async Task CanAddNodesAndRelationships()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice", LastName = "Smith" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob", LastName = "Jones" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow, IsBidirectional = true };
        alice.Knows.Add(knows);

        await this.Graph.CreateNode(alice, new GraphOperationOptions { CreateMissingNodes = true, TraversalDepth = 1 });

        // Query Alice and include her friends via Knows
        var aliceFromProvider = await this.Graph.GetNode<PersonWithNavigationProperty>(alice.Id, new GraphOperationOptions { TraversalDepth = -1 });

        Assert.NotNull(aliceFromProvider);
        Assert.Equal("Alice", aliceFromProvider.FirstName);
        Assert.Equal(alice.Id, aliceFromProvider.Id);
        Assert.NotNull(aliceFromProvider.Knows);
        // Check navigation property
        Assert.Contains(aliceFromProvider.Knows, k => k.Target!.FirstName == "Bob");

        // Get Bob
        var bobFromProvider = await this.Graph.GetNode<PersonWithNavigationProperty>(bob.Id, new GraphOperationOptions { TraversalDepth = -1 });
        Assert.NotNull(bobFromProvider);
        Assert.Equal("Bob", bobFromProvider.FirstName);
        Assert.Equal(bob.Id, bobFromProvider.Id);
        // Check navigation property
        Assert.Contains(bobFromProvider.Knows, k => k.Target!.FirstName == "Alice");
    }

    public class PersonWithINodeProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Person? Friend { get; set; } = null;
    }

    [Fact]
    public async Task CannotAddNodeWithINodeProperty()
    {
        var person = new PersonWithINodeProperty { FirstName = "A", LastName = "B", Friend = new Person { FirstName = "C", LastName = "D" } };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }
}
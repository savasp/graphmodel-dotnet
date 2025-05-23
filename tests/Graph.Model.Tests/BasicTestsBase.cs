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

public abstract class BasicTestsBase
{
    private IGraph provider;

    protected BasicTestsBase(IGraph provider)
    {
        this.provider = provider;
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithPrimitiveProperties()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.provider.CreateNode(person);
        var fetched = await this.provider.GetNode<Person>(person.Id);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperties()
    {
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe" };
        await this.provider.CreateNode(person);
        var fetched = await this.provider.GetNode<PersonWithComplexProperty>(person.Id);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CannotCreateRelationshipWithComplexProperties()
    {
        var p1 = new PersonWithComplexProperty { FirstName = "A", Address = new Address { Street = "123 Main St", City = "Somewhere" } };
        var p2 = new PersonWithComplexProperty { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var address = new Address { Street = "123 Main St", City = "Somewhere" };
        var knows = new KnowsWithComplexProperty(p1, p2) { MetAt = address };
        await Assert.ThrowsAsync<GraphException>(() => this.provider.CreateRelationship(knows));
    }

    [Fact]
    public async Task CanCreateAndGetRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };

        await this.provider.CreateRelationship(knows);

        var fetched = await this.provider.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(p1.Id, fetched.SourceId);
        Assert.Equal(p2.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanCreateRelationshipAndAddNodesAtTheSameTime()
    {
        var p1 = new PersonWithComplexProperty { FirstName = "A", Address = new Address { Street = "123 Main St", City = "Somewhere" } };
        var p2 = new PersonWithComplexProperty { FirstName = "B", Address = new Address { Street = "456 Elm St", City = "Anywhere" } };

        var knows = new KnowsWithComplexProperty(p1, p2) { Since = DateTime.UtcNow, MetAt = new Address { Street = "789 Oak St", City = "Everywhere" } };

        // This should add the nodes and create the relationship
        await this.provider.CreateRelationship(knows);

        var fetched = await this.provider.GetRelationship<KnowsWithComplexProperty>(knows.Id);
        Assert.Equal(p1.Id, fetched.SourceId);
        Assert.Equal(p2.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanUpdateNode()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.provider.CreateNode(person);
        person.LastName = "Smith";

        await this.provider.UpdateNode(person);

        var updated = await this.provider.GetNode<Person>(person.Id);

        Assert.Equal("Smith", updated.LastName);
    }

    [Fact]
    public async Task CanCreateAndDeleteNode()
    {
        var person = new Person { FirstName = "ToDelete" };
        await this.provider.CreateNode(person);
        await this.provider.DeleteNode(person.Id);
        await Assert.ThrowsAsync<GraphException>(() => this.provider.GetNode<Person>(person.Id));
    }

    [Fact]
    public async Task CanCreateAndDeleteRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };

        await this.provider.CreateRelationship(knows);
        await this.provider.DeleteRelationship(knows.Id);
        await Assert.ThrowsAsync<GraphException>(() => this.provider.GetRelationship<Knows<Person, Person>>(knows.Id));
    }

    [Fact]
    public async Task CanCreatePersonWithFriendExample()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        var friend = new Person { FirstName = "Jane", LastName = "Smith" };
        await this.provider.CreateNode(person);
        await this.provider.CreateNode(friend);
        var knows = new Knows<Person, Person>(person, friend) { Since = DateTime.Now };
        await this.provider.CreateRelationship(knows);
        var fetched = await this.provider.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(person.Id, fetched.SourceId);
        Assert.Equal(friend.Id, fetched.TargetId);
    }

    [Fact]
    public async Task CanGetMultipleNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        var ids = new[] { p1.Id, p2.Id };
        var fetched = await this.provider.GetNodes<Person>(ids);
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
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);
        await this.provider.CreateNode(p3);
        var knows1 = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };
        var knows2 = new Knows<Person, Person>(p2, p3) { Since = DateTime.UtcNow };
        await this.provider.CreateRelationship(knows1);
        await this.provider.CreateRelationship(knows2);
        var rels = await this.provider.GetRelationships<Knows<Person, Person>>([knows1.Id, knows2.Id]);
        Assert.Equal(2, ((ICollection<Knows<Person, Person>>)rels).Count);
        Assert.Contains(rels, r => r.Id == knows1.Id);
        Assert.Contains(rels, r => r.Id == knows2.Id);
    }

    [Fact]
    public async Task CanUpdateRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.provider.CreateNode(p1);
        await this.provider.CreateNode(p2);

        var knows = new Knows<Person, Person>(p1, p2) { Since = DateTime.UtcNow };
        await this.provider.CreateRelationship(knows);
        knows.Since = DateTime.UtcNow.AddYears(-1);
        await this.provider.UpdateRelationship(knows);
        var updated = await this.provider.GetRelationship<Knows<Person, Person>>(knows.Id);
        Assert.Equal(knows.Id, updated.Id);
        Assert.Equal(p1.Id, updated.SourceId);
        Assert.Equal(p2.Id, updated.TargetId);
        Assert.Equal(knows.Since, updated.Since);
    }

    [Fact]
    public async Task CanBeginTransactionAndRollback()
    {
        var tx = await this.provider.BeginTransaction();
        var person = new Person { FirstName = "TxTest" };
        await this.provider.CreateNode(person, new(), tx);
        await tx.DisposeAsync(); // Rollback
        await Assert.ThrowsAsync<GraphException>(() => this.provider.GetNode<Person>(person.Id));
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

        await Assert.ThrowsAsync<GraphException>(() => this.provider.CreateNode(person));
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

        await this.provider.CreateNode(person);
    }

    public class PersonWithNonListGenericProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Dictionary<string, Person> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CannotAddNodeWithNonListGenericProperty()
    {
        var person = new PersonWithNonListGenericProperty { FirstName = "A", GenericProperty = new Dictionary<string, Person>() };

        await Assert.ThrowsAsync<GraphException>(() => this.provider.CreateNode(person));
    }

    [Fact]
    public void CanQueryNodesLinq()
    {
        var queryable = this.provider.Nodes<Person>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public void CanQueryRelationshipsLinq()
    {
        var queryable = this.provider.Relationships<Knows<Person, Person>>();
        Assert.NotNull(queryable);
    }

    [Fact]
    public async Task CanAddNodesAndRelationships()
    {
        var alice = new PersonWithNavigationProperty { FirstName = "Alice", LastName = "Smith" };
        var bob = new PersonWithNavigationProperty { FirstName = "Bob", LastName = "Jones" };
        var knows = new Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>(alice, bob) { Since = DateTime.UtcNow };
        alice.Knows.Add(knows);

        await this.provider.CreateNode(alice, new GraphOperationOptions { CreateMissingNodes = true });

        // Query Alice and include her friends via Knows
        var aliceFromProvider = await this.provider.GetNode<PersonWithNavigationProperty>(alice.Id, new GraphOperationOptions { TraversalDepth = -1 });

        Assert.NotNull(aliceFromProvider);
        Assert.Equal("Alice", aliceFromProvider.FirstName);
        Assert.Equal(alice.Id, aliceFromProvider.Id);
        Assert.NotNull(aliceFromProvider.Knows);
        // Check navigation property
        Assert.Contains(aliceFromProvider.Knows, k => k.Target.FirstName == "Bob");

        // Get Bob
        var bobFromProvider = await this.provider.GetNode<PersonWithNavigationProperty>(bob.Id, new GraphOperationOptions { TraversalDepth = -1 });
        Assert.NotNull(bobFromProvider);
        Assert.Equal("Bob", bobFromProvider.FirstName);
        Assert.Equal(bob.Id, bobFromProvider.Id);
        // Check navigation property
        Assert.Contains(bobFromProvider.Knows, k => k.Target.FirstName == "Alice");
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
        await Assert.ThrowsAsync<GraphException>(() => this.provider.CreateNode(person));
    }
}
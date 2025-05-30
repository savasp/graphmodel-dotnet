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
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe", Address = new Address { Street = "123 Main St", City = "Somewhere" } };
        await this.Graph.CreateNode(person);
        var fetched = await this.Graph.GetNode<PersonWithComplexProperty>(person.Id);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
        Assert.Equal("123 Main St", fetched.Address.Street);
        Assert.Equal("Somewhere", fetched.Address.City);
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
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

        var dateTime = DateTime.UtcNow;
        var knows = new Knows { SourceId = p1.Id, TargetId = p2.Id, Since = dateTime };

        await this.Graph.CreateRelationship(knows);

        var fetched = await this.Graph.GetRelationship<Knows>(knows.Id);
        Assert.Equal(p1.Id, fetched.SourceId);
        Assert.Equal(p2.Id, fetched.TargetId);
        Assert.Equal(dateTime, fetched.Since);
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

        var knows = new Knows { SourceId = p1.Id, TargetId = p2.Id, Since = DateTime.UtcNow };

        await this.Graph.CreateRelationship(knows);
        await this.Graph.DeleteRelationship(knows.Id);
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetRelationship<Knows>(knows.Id));
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
        var knows1 = new Knows { SourceId = p1.Id, TargetId = p2.Id, Since = DateTime.UtcNow };
        var knows2 = new Knows { SourceId = p2.Id, TargetId = p3.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationship(knows1);
        await this.Graph.CreateRelationship(knows2);
        var rels = await this.Graph.GetRelationships<Knows>([knows1.Id, knows2.Id]);
        Assert.Equal(2, ((ICollection<Knows>)rels).Count);
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

        var knows = new Knows { SourceId = p1.Id, TargetId = p2.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationship(knows);
        knows.Since = DateTime.UtcNow.AddYears(-1);
        await this.Graph.UpdateRelationship(knows);
        var updated = await this.Graph.GetRelationship<Knows>(knows.Id);
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
        await this.Graph.CreateNode(person, tx);
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

    public class PersonWithGenericCollectionOfPrimitiveProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CanAddAndGetNodeWithGenericCollectionOfPrimitiveProperty()
    {
        var person = new PersonWithGenericCollectionOfPrimitiveProperty { FirstName = "A", GenericProperty = new List<string> { "B", "C" } };

        await this.Graph.CreateNode(person);

        var fetched = await this.Graph.GetNode<PersonWithGenericCollectionOfPrimitiveProperty>(person.Id);
        Assert.Equal("A", fetched.FirstName);
        Assert.Equal(person.GenericProperty.Count, fetched.GenericProperty.Count);
        Assert.All(fetched.GenericProperty, item => Assert.Contains(item, person.GenericProperty));
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
    public void CanCreateRelationshipsQuery()
    {
        var queryable = this.Graph.Relationships<Knows>();
        Assert.NotNull(queryable);
    }

    public class PersonWithINodeProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Person? Friend { get; set; } = null;
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithINodeProperty()
    {
        var person = new PersonWithINodeProperty { FirstName = "A", LastName = "B", Friend = new Person { FirstName = "C", LastName = "D" } };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithINodePropertyInComplexProperty : Node
    {
        public class FooComplexType
        {
            public Person? Bar { get; set; } = null;
        }

        public FooComplexType Foo { get; set; } = new();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithINodePropertyInComplexProperty()
    {
        var person = new PersonWithINodePropertyInComplexProperty
        {
            FirstName = "A",
            LastName = "B",
            Foo = new()
            {
                Bar = new Person { FirstName = "C", LastName = "D" }
            }
        };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithIRelationshipPropertyInComplexProperty : Node
    {
        public class FooComplexType
        {
            public Knows? Knows { get; set; } = null;
        }

        public FooComplexType Foo { get; set; } = new();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithIRelationshipPropertyInComplexProperty()
    {
        var person = new PersonWithIRelationshipPropertyInComplexProperty
        {
            FirstName = "A",
            LastName = "B",
            Foo = new()
            {
                Knows = new Knows { SourceId = "1", TargetId = "2", Since = DateTime.UtcNow }
            }
        };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithListIRelationshipPropertyInComplexProperty : Node
    {
        public class FooComplexType
        {
            public List<Knows> Knows { get; set; } = new();
        }

        public FooComplexType Foo { get; set; } = new();
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithListIRelationshipPropertyInComplexProperty()
    {
        var person = new PersonWithListIRelationshipPropertyInComplexProperty
        {
            FirstName = "A",
            LastName = "B",
            Foo = new()
            {
                Knows = [new Knows { SourceId = "1", TargetId = "2", Since = DateTime.UtcNow }]
            }
        };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithListINodeProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<Person> Friends { get; set; } = new List<Person>();
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithListINodeProperty()
    {
        var person = new PersonWithListINodeProperty { FirstName = "A", LastName = "B" };
        person.Friends.Add(new Person { FirstName = "C", LastName = "D" });
        person.Friends.Add(new Person { FirstName = "E", LastName = "F" });
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }

    public class PersonWithIRelationshipProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Person? Friend { get; set; } = null;
    }

    [Fact(Skip = "We are going to implement a Roslyn analyzer to catch this at compile time. We need to decide whether to also have a runtime check. The check was there but has been removed. Hence why we are skipping this test for now.")]
    public async Task CannotAddNodeWithRelationshipProperty()
    {
        var person = new PersonWithIRelationshipProperty { FirstName = "A", LastName = "B", Friend = new Person { FirstName = "C", LastName = "D" } };
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNode(person));
    }
}
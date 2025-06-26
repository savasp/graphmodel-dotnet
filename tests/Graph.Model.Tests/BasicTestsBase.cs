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
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
    }

    [Fact]
    public async Task CanCreateAndGetNodeWithComplexProperties()
    {
        var person = new PersonWithComplexProperty { FirstName = "John", LastName = "Doe", Address = new AddressValue { Street = "123 Main St", City = "Somewhere" } };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        var fetched = await this.Graph.GetNodeAsync<PersonWithComplexProperty>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("John", fetched.FirstName);
        Assert.Equal("Doe", fetched.LastName);
        Assert.Equal("123 Main St", fetched.Address.Street);
        Assert.Equal("Somewhere", fetched.Address.City);
    }


    [Fact]
    public async Task CanCreateAndGetRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var dateTime = DateTime.UtcNow;
        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = dateTime };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(p1.Id, fetched.StartNodeId);
        Assert.Equal(p2.Id, fetched.EndNodeId);
        Assert.Equal(dateTime, fetched.Since);
    }

    [Fact]
    public async Task CanUpdateNode()
    {
        var person = new Person { FirstName = "John", LastName = "Doe" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        person.LastName = "Smith";

        await this.Graph.UpdateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var updated = await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal("Smith", updated.LastName);
    }

    [Fact]
    public async Task CanCreateAndDeleteNode()
    {
        var person = new Person { FirstName = "ToDelete" };
        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        await this.Graph.DeleteNodeAsync(person.Id, false, null, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanCreateAndDeleteRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        await this.Graph.DeleteRelationshipAsync(knows.Id, null, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanGetMultipleNodes()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        var ids = new[] { p1.Id, p2.Id };
        var fetched = await this.Graph.Nodes<Person>().Where(x => ids.Contains(x.Id)).ToListAsync(TestContext.Current.CancellationToken);
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
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);
        var knows1 = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };
        var knows2 = new Knows { StartNodeId = p2.Id, EndNodeId = p3.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);
        var rels = await this.Graph.Relationships<Knows>()
            .Where(r => r.StartNodeId == p1.Id || r.StartNodeId == p2.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rels.Count);
        Assert.Contains(rels, r => r.Id == knows1.Id);
        Assert.Contains(rels, r => r.Id == knows2.Id);
    }

    [Fact]
    public async Task CanUpdateRelationship()
    {
        var p1 = new Person { FirstName = "A" };
        var p2 = new Person { FirstName = "B" };
        await this.Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = p1.Id, EndNodeId = p2.Id, Since = DateTime.UtcNow };
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        knows.Since = DateTime.UtcNow.AddYears(-1);
        await this.Graph.UpdateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);
        var updated = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(knows.Id, updated.Id);
        Assert.Equal(p1.Id, updated.StartNodeId);
        Assert.Equal(p2.Id, updated.EndNodeId);
        Assert.Equal(knows.Since, updated.Since);
    }

    [Fact]
    public async Task CanBeginTransactionAndRollback()
    {
        var tx = await this.Graph.GetTransactionAsync();
        var person = new Person { FirstName = "TxTest" };
        await this.Graph.CreateNodeAsync(person, tx, TestContext.Current.CancellationToken);
        await tx.DisposeAsync(); // Rollback
        await Assert.ThrowsAsync<GraphException>(() => this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken));
    }

    public record PersonWithCycle : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Foo Foo { get; set; } = new();
    }

    public record Foo
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

        await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken));
    }

    public record PersonWithGenericCollectionOfPrimitiveProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> GenericProperty { get; set; } = [];
    }

    [Fact]
    public async Task CanAddAndGetNodeWithGenericCollectionOfPrimitiveProperty()
    {
        var person = new PersonWithGenericCollectionOfPrimitiveProperty { FirstName = "A", GenericProperty = ["B", "C"] };

        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var fetched = await this.Graph.GetNodeAsync<PersonWithGenericCollectionOfPrimitiveProperty>(person.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("A", fetched.FirstName);
        Assert.Equal(person.GenericProperty.Count, fetched.GenericProperty.Count);
        Assert.All(fetched.GenericProperty, item => Assert.Contains(item, person.GenericProperty));
    }

    public record PersonWithGenericDictionaryProperty : Node
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        // TODO: Add serialization support for dictionaries.
        //public Dictionary<string, Person> GenericProperty { get; set; } = [];
    }

    /*
        [Fact]
        public async Task CannotAddNodeWithGenericDictionaryProperty()
        {
            var person = new PersonWithGenericDictionaryProperty { FirstName = "A", GenericProperty = new Dictionary<string, Person>() };

            await Assert.ThrowsAsync<GraphException>(() => this.Graph.CreateNodeAsync(person));
        }
    */

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
}
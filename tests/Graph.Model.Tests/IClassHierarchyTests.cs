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

public interface IClassHierarchyTests : IGraphModelTest
{
    [Fact]
    public async Task CanCreateAndRetrieveNodeViaBaseType()
    {
        var manager = new Manager
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 40,
            Department = "Engineering",
            TeamSize = 10
        };

        await this.Graph.CreateNodeAsync(manager, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetNodeAsync<Person>(manager.Id, null, TestContext.Current.CancellationToken);

        // Even though we are retrieving as Person, we should still get the full Manager object
        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, ((Manager)retrieved).Department);
        Assert.Equal(manager.TeamSize, ((Manager)retrieved).TeamSize);
    }

    [Fact]
    public async Task CanCreateNodeViaBaseTypeAndRetrieveItViaDerivedType()
    {
        var manager = new Manager
        {
            FirstName = "Jane",
            LastName = "Smith",
            Age = 35,
            Department = "Marketing",
            TeamSize = 5
        };

        Person person = manager; // Implicit conversion to base type

        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetNodeAsync<Manager>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, retrieved.Department);
        Assert.Equal(manager.TeamSize, retrieved.TeamSize);
    }

    [Fact]
    public async Task CanCreateNodeViaBaseTypeAndRetrieveItViaBaseType()
    {
        var manager = new Manager
        {
            FirstName = "Jane",
            LastName = "Smith",
            Age = 35,
            Department = "Marketing",
            TeamSize = 5
        };

        Person person = manager; // Implicit conversion to base type

        await this.Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetNodeAsync<Person>(person.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, ((Manager)retrieved).Department);
        Assert.Equal(manager.TeamSize, ((Manager)retrieved).TeamSize);
    }

    [Fact]
    public async Task CanCreateAndRetrieveRelationshipViaBaseType()
    {
        var person1 = new Person
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Age = 28
        };
        var person2 = new Person
        {
            FirstName = "Bob",
            LastName = "Smith",
            Age = 32
        };
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        var knowsWell = new KnowsWell(person1, person2)
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.CreateRelationshipAsync(knowsWell, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetRelationshipAsync<Knows>(knowsWell.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.StartNodeId, retrieved.StartNodeId);
        Assert.Equal(knowsWell.EndNodeId, retrieved.EndNodeId);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, ((KnowsWell)retrieved).HowWell);
    }

    [Fact]
    public async Task CanCreateRelationshipViaBaseTypeAndRetrieveItViaDerivedType()
    {
        var person1 = new Person
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Age = 28
        };
        var person2 = new Person
        {
            FirstName = "Bob",
            LastName = "Smith",
            Age = 32
        };
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        var knowsWell = new KnowsWell(person1, person2)
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type

        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetRelationshipAsync<KnowsWell>(knowsWell.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.StartNodeId, retrieved.StartNodeId);
        Assert.Equal(knowsWell.EndNodeId, retrieved.EndNodeId);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, retrieved.HowWell);
    }

    [Fact]
    public async Task CanCreateRelationshipViaBaseTypeAndRetrieveItViaBaseType()
    {
        var person1 = new Person
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Age = 28
        };
        var person2 = new Person
        {
            FirstName = "Bob",
            LastName = "Smith",
            Age = 32
        };
        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        var knowsWell = new KnowsWell(person1, person2)
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type
        await this.Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.GetRelationshipAsync<Knows>(knows.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.StartNodeId, retrieved.StartNodeId);
        Assert.Equal(knowsWell.EndNodeId, retrieved.EndNodeId);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, ((KnowsWell)retrieved).HowWell);
    }

    [Fact]
    public async Task CanQueryNodeUsingBaseType()
    {
        var manager = new Manager
        {
            FirstName = "Jane",
            LastName = "Smith",
            Age = 35,
            Department = "Marketing",
            TeamSize = 5
        };

        await this.Graph.CreateNodeAsync(manager, null, TestContext.Current.CancellationToken);

        var retrieved = this.Graph.Nodes<Person>()
            .FirstOrDefault();

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.Id, retrieved.Id);
        Assert.Equal(manager.FirstName, retrieved.FirstName);
        Assert.Equal(manager.LastName, retrieved.LastName);
        Assert.Equal(manager.Age, retrieved.Age);
        Assert.Equal(manager.Department, ((Manager)retrieved).Department);
        Assert.Equal(manager.TeamSize, ((Manager)retrieved).TeamSize);
    }

    [Fact]
    public async Task CanQueryRelationshipUsingBaseType()
    {
        var person1 = new Person
        {
            FirstName = "Alice",
            LastName = "Johnson",
            Age = 28
        };
        var person2 = new Person
        {
            FirstName = "Bob",
            LastName = "Smith",
            Age = 32
        };

        var knowsWell = new KnowsWell(person1, person2)
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(knowsWell, null, TestContext.Current.CancellationToken);

        var retrieved = this.Graph.Relationships<Knows>()
            .FirstOrDefault();

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.StartNodeId, retrieved.StartNodeId);
        Assert.Equal(knowsWell.EndNodeId, retrieved.EndNodeId);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, ((KnowsWell)retrieved).HowWell);
    }

    [Fact]
    public async Task CanQueryOverAnyRelationship()
    {
        // Setup
        var alice = new Person { FirstName = "Alice" };
        var bob = new Person { FirstName = "Bob" };
        var charlie = new Person { FirstName = "Charlie" };
        var dave = new Person { FirstName = "Dave" };

        await this.Graph.CreateNodeAsync(alice, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(bob, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(charlie, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(dave, null, TestContext.Current.CancellationToken);

        // Alice knows everyone, Bob knows 2, Charlie knows 1, Dave knows none
        await this.Graph.CreateRelationshipAsync(new Knows(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(alice, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(bob, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Knows(charlie, dave), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(alice, bob), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(bob, charlie), null, TestContext.Current.CancellationToken);
        await this.Graph.CreateRelationshipAsync(new Friend(charlie, dave), null, TestContext.Current.CancellationToken);

        // Get Alice's relationships
        var connectionStats = await this.Graph.Nodes<Person>()
            .PathSegments<Person, IRelationship, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(9, connectionStats.Count);

        var aliceRelationships = connectionStats.Count(ps => ps.StartNode.FirstName == "Alice");
        Assert.Equal(4, aliceRelationships);

        var bobRelationships = connectionStats.Count(ps => ps.StartNode.FirstName == "Bob");
        Assert.Equal(3, bobRelationships);

        var charlieRelationships = connectionStats.Count(ps => ps.StartNode.FirstName == "Charlie");
        Assert.Equal(2, charlieRelationships);

        var daveRelationships = connectionStats.Count(ps => ps.StartNode.FirstName == "Dave");
        Assert.Equal(0, daveRelationships);
    }
}
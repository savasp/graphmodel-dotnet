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

public abstract class ClassHierarchyTestsBase : ITestBase
{
    public abstract IGraph Graph { get; }

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

        await this.Graph.CreateNodeAsync(manager);

        var retrieved = await this.Graph.GetNodeAsync<Person>(manager.Id);

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

        await this.Graph.CreateNodeAsync(person);

        var retrieved = await this.Graph.GetNodeAsync<Manager>(person.Id);

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

        await this.Graph.CreateNodeAsync(person);

        var retrieved = await this.Graph.GetNodeAsync<Person>(person.Id);

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
        await this.Graph.CreateNodeAsync(person1);
        await this.Graph.CreateNodeAsync(person2);
        var knowsWell = new KnowsWell(person1, person2)
        {
            IsBidirectional = true,
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.CreateRelationshipAsync(knowsWell);

        var retrieved = await this.Graph.GetRelationshipAsync<Knows>(knowsWell.Id);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.SourceId, retrieved.SourceId);
        Assert.Equal(knowsWell.TargetId, retrieved.TargetId);
        Assert.Equal(knowsWell.IsBidirectional, retrieved.IsBidirectional);
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
        await this.Graph.CreateNodeAsync(person1);
        await this.Graph.CreateNodeAsync(person2);
        var knowsWell = new KnowsWell(person1, person2)
        {
            IsBidirectional = true,
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type

        await this.Graph.CreateRelationshipAsync(knows);

        var retrieved = await this.Graph.GetRelationshipAsync<KnowsWell>(knowsWell.Id);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.SourceId, retrieved.SourceId);
        Assert.Equal(knowsWell.TargetId, retrieved.TargetId);
        Assert.Equal(knowsWell.IsBidirectional, retrieved.IsBidirectional);
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
        await this.Graph.CreateNodeAsync(person1);
        await this.Graph.CreateNodeAsync(person2);
        var knowsWell = new KnowsWell(person1, person2)
        {
            IsBidirectional = true,
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type
        await this.Graph.CreateRelationshipAsync(knows);

        var retrieved = await this.Graph.GetRelationshipAsync<Knows>(knows.Id);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.SourceId, retrieved.SourceId);
        Assert.Equal(knowsWell.TargetId, retrieved.TargetId);
        Assert.Equal(knowsWell.IsBidirectional, retrieved.IsBidirectional);
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

        await this.Graph.CreateNodeAsync(manager);

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
            IsBidirectional = true,
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.CreateNodeAsync(person1);
        await this.Graph.CreateNodeAsync(person2);
        await this.Graph.CreateRelationshipAsync(knowsWell);

        var retrieved = this.Graph.Relationships<Knows>()
            .FirstOrDefault();

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.Id, retrieved.Id);
        Assert.Equal(knowsWell.SourceId, retrieved.SourceId);
        Assert.Equal(knowsWell.TargetId, retrieved.TargetId);
        Assert.Equal(knowsWell.IsBidirectional, retrieved.IsBidirectional);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, ((KnowsWell)retrieved).HowWell);
    }

}
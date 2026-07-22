// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IClassHierarchyTests : IGraphTest
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

        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Person>(manager.TestKey, null, TestContext.Current.CancellationToken);

        // Even though we are retrieving as Person, we should still get the full Manager object
        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.TestKey, retrieved.TestKey);
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

        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Manager>(person.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.TestKey, retrieved.TestKey);
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

        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Person>(person.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<Manager>(retrieved);
        Assert.Equal(manager.TestKey, retrieved.TestKey);
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
        var knowsWell = new KnowsWell
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.ConnectAsync(person1, knowsWell, person2, cancellationToken: TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knowsWell.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.TestKey, retrieved.TestKey);
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
        var knowsWell = new KnowsWell
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type

        await this.Graph.ConnectAsync(person1, knows, person2, cancellationToken: TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.FindRelationshipByTestKeyAsync<KnowsWell>(knowsWell.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.TestKey, retrieved.TestKey);
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
        var knowsWell = new KnowsWell
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        Knows knows = knowsWell; // Implicit conversion to base type
        await this.Graph.ConnectAsync(person1, knows, person2, cancellationToken: TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.FindRelationshipByTestKeyAsync<Knows>(knows.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.TestKey, retrieved.TestKey);
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
        Assert.Equal(manager.TestKey, retrieved.TestKey);
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

        var knowsWell = new KnowsWell
        {
            Since = DateTime.UtcNow,
            HowWell = "Very well"
        };

        await this.Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await this.Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(person1, knowsWell, person2, cancellationToken: TestContext.Current.CancellationToken);

        var retrieved = this.Graph.Relationships<Knows>()
            .FirstOrDefault();

        Assert.NotNull(retrieved);
        Assert.IsType<KnowsWell>(retrieved);
        Assert.Equal(knowsWell.TestKey, retrieved.TestKey);
        Assert.Equal(knowsWell.Since, retrieved.Since);
        Assert.Equal(knowsWell.HowWell, ((KnowsWell)retrieved).HowWell);
    }

    [Fact]
    public async Task CanCreateAndRetrieveThreeLevelHierarchyNodeViaBaseType()
    {
        var policeDog = new PoliceDog
        {
            Name = "K9",
            Breed = "Shepherd",
            Badge = "K9-42",
        };

        await this.Graph.CreateNodeAsync(policeDog, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Animal>(policeDog.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        var typedRetrieved = Assert.IsType<PoliceDog>(retrieved);
        Assert.Equal(policeDog.TestKey, typedRetrieved.TestKey);
        Assert.Equal(policeDog.Name, typedRetrieved.Name);
        Assert.Equal(policeDog.Breed, typedRetrieved.Breed);
        Assert.Equal(policeDog.Badge, typedRetrieved.Badge);
    }

    [Fact]
    public async Task CanCreateAndRetrieveThreeLevelHierarchyNodeViaMidHierarchyType()
    {
        var policeDog = new PoliceDog
        {
            Name = "K9",
            Breed = "Shepherd",
            Badge = "K9-42",
        };

        await this.Graph.CreateNodeAsync(policeDog, null, TestContext.Current.CancellationToken);

        // Request via the mid-hierarchy type (Dog), still get the most-derived PoliceDog back.
        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Dog>(policeDog.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        var typedRetrieved = Assert.IsType<PoliceDog>(retrieved);
        Assert.Equal(policeDog.TestKey, typedRetrieved.TestKey);
        Assert.Equal(policeDog.Name, typedRetrieved.Name);
        Assert.Equal(policeDog.Breed, typedRetrieved.Breed);
        Assert.Equal(policeDog.Badge, typedRetrieved.Badge);
    }

    [Fact]
    public async Task CanCreateAndRetrieveThreeLevelHierarchyNodeViaOwnType()
    {
        var dog = new Dog
        {
            Name = "Rex",
            Breed = "Labrador",
        };

        await this.Graph.CreateNodeAsync(dog, null, TestContext.Current.CancellationToken);

        var retrieved = await this.Graph.FindNodeByTestKeyAsync<Animal>(dog.TestKey, null, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        var typedRetrieved = Assert.IsType<Dog>(retrieved);
        Assert.Equal(dog.TestKey, typedRetrieved.TestKey);
        Assert.Equal(dog.Name, typedRetrieved.Name);
        Assert.Equal(dog.Breed, typedRetrieved.Breed);
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
        await this.Graph.ConnectAsync(alice, new Knows(), bob, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(alice, new Knows(), charlie, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(alice, new Knows(), dave, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(bob, new Knows(), charlie, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(bob, new Knows(), dave, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(charlie, new Knows(), dave, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(alice, new Friend(), bob, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(bob, new Friend(), charlie, cancellationToken: TestContext.Current.CancellationToken);
        await this.Graph.ConnectAsync(charlie, new Friend(), dave, cancellationToken: TestContext.Current.CancellationToken);

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

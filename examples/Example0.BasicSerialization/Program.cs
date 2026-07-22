// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 1: Basic CRUD Operations
// Demonstrates fundamental create, read, update, delete operations with nodes and relationships

Console.WriteLine("=== Example 0: Basic Serialization ===\n");

const string databaseName = "example0";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
}

Console.WriteLine($"✓ Created database: {databaseName}");

// We start with the Neo4j Graph Provider here

// Create graph instance
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null, loggerFactory);
var graph = store.Graph;

try
{
    // ==== CREATE OPERATIONS ====
    Console.WriteLine("1. Creating nodes...");

    // Create employees
    var alice = new Person
    {
        Name = "Alice Johnson",
        Email = "alice@techcorp.com",
        Age = 30,
        Department = "Engineering",
        Skills = new List<string> { "C#", "Neo4j", "Microservices" },
        KeyDates = new List<DateTime> { new DateTime(2020, 1, 1), new DateTime(2021, 6, 1) },
        SomeNumbers = new List<int> { 1, 2, 3 },
        EmotionalStates = new List<EmotionalState> { EmotionalState.Happy, EmotionalState.Curious }
    };

    var bob = new Person
    {
        Name = "Bob Smith",
        Email = "bob@techcorp.com",
        Age = 28,
        Department = "Marketing",
        Skills = new List<string> { "Marketing", "SEO", "Content Creation" },
        KeyDates = new List<DateTime> { new DateTime(2020, 2, 1), new DateTime(2021, 7, 1) },
        SomeNumbers = new List<int> { 4, 5, 6 },
        EmotionalStates = new List<EmotionalState> { EmotionalState.Sad, EmotionalState.Anxious }
    };

    // Save nodes to graph
    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);

    Console.WriteLine($"✓ Created employee: {alice.Name}");
    Console.WriteLine($"✓ Created employee: {bob.Name}\n");

    var springfield = new City { Name = "Springfield" };
    // Create a node with complex a complex property
    var charlie = new PersonWithComplex
    {
        Name = "Charlie Brown",
        Email = "charlie@techcorp.com",
        Age = 35,
        Department = "Design",
        HomeAddress = new Address
        {
            Street = "456 Elm St",
            City = springfield,
            State = State.CA,
            ZipCode = "62704",
            Country = "USA",
            Aliases = new List<string> { "Home", "Work" }
        },
        WorkAddress = new Address
        {
            Street = "789 Oak St",
            City = springfield,
            State = State.CA,
            ZipCode = "62704",
            Country = "USA",
            Aliases = new List<string> { "Office", "HQ" }
        }
    };

    // Save complex node to graph
    await graph.CreateNodeAsync(charlie);
    Console.WriteLine($"✓ Created employee with address: {charlie.Name}");
    Console.WriteLine($"  Home Address: {charlie.HomeAddress.Street}, {charlie.HomeAddress.City.Name}, {charlie.HomeAddress.State} {charlie.HomeAddress.ZipCode}, {charlie.HomeAddress.Country}");
    Console.WriteLine($"  Work Address: {charlie.WorkAddress.Street}, {charlie.WorkAddress.City.Name}, {charlie.WorkAddress.State} {charlie.WorkAddress.ZipCode}, {charlie.WorkAddress.Country}\n");

    var seattle = new City { Name = "Seattle" };
    var foo = new Foo
    {
        Name = "Shared Foo",
        Value = 100
    };

    // Create a node with a list of complex properties
    var dave = new PersonWithComplex
    {
        Name = "Dave Wilson",
        Email = "dave@techcorp.com",
        Age = 40,
        Department = "Sales",
        Foo = foo, // Reference to shared Foo
        HomeAddress = new Address
        {
            Street = "123 Main St",
            City = seattle,
            State = State.WA,
            ZipCode = "62704",
            Country = "USA",
            Aliases = new List<string> { "Home", "Personal" }
        },
        PreviousAddresses = new List<Address>
        {
            new Address
            {
                Street = "789 Oak St",
                City = seattle,
                State = State.WA,
                ZipCode = "62704",
                Country = "USA",
                Aliases = new List<string> { "Home", "HQ" }
            },
            new Address
            {
                Street = "101 Pine St",
                City = seattle,
                State = State.OR,
                ZipCode = "62704",
                Country = "USA",
                Aliases = new List<string> { "Office", "Branch" }
            }
        }
    };

    var elen = new PersonWithComplex
    {
        Name = "Elen Smith",
        Email = "elen@techcorp.com",
        Age = 32,
        Department = "HR",
        HomeAddress = new Address
        {
            Street = "321 Oak St",
            City = seattle,
            State = State.CA,
            ZipCode = "62704",
            Country = "USA",
            Aliases = new List<string> { "Home", "Personal" }
        },
        PreviousAddresses = new List<Address>
        {
            new Address
            {
                Street = "654 Maple St",
                City = seattle,
                State = State.CA,
                ZipCode = "62704",
                Country = "USA",
                Aliases = new List<string> { "Old Home", "Childhood" }
            }
        },
        Foo = new Foo
        {
            Name = "Elen's Foo",
            Value = 42,
            ImportantDates = new List<DateTime> { new DateTime(2022, 1, 1), new DateTime(2023, 6, 15) },
            Bar = new Bar
            {
                Description = "Elen's Bar",
                Numbers = new List<int> { 7, 8, 9 },
                Foo = foo, // Reference to shared Foo
                Baz = new Baz
                {
                    Title = "Elen's Baz",
                    Tags = new List<string> { "Tag1", "Tag2" },
                }
            }
        },
    };

    // Save the node with a list of addresses
    Console.WriteLine("2. Creating employee with multiple addresses...");
    await graph.CreateNodeAsync(dave);
    await graph.CreateNodeAsync(elen);

    Console.WriteLine($"✓ Created employee with multiple addresses: {dave.Name}");
    foreach (var address in dave.PreviousAddresses)
    {
        Console.WriteLine($"  Address: {address.Street}, {address.City.Name}, {address.State} {address.ZipCode}, {address.Country}");
    }

    // Serialize a relationship
    Console.WriteLine("3. Creating a relationship between Alice and Bob...");
    var aliceFriendBob = new Friend { Since = new DateTime(2021, 1, 1) };
    await graph.CreateRelationshipAsync(
        graph.Nodes<Person>().Where(person => person.Email == alice.Email),
        aliceFriendBob,
        graph.Nodes<Person>().Where(person => person.Email == bob.Email));

    Console.WriteLine($"✓ Created relationship between {alice.Name} and {bob.Name}");
    Console.WriteLine($"  Since: {aliceFriendBob.Since}");

    // Work with DynamicNode and DynamicRelationship
    Console.WriteLine("4. Creating a dynamic node and relationship...");
    var person1 = new DynamicNode(["DynamicPerson", "Manager"], new Dictionary<string, object?>
    {
        { "Name", "Dynamic Dave" },
        { "Role", "Manager" },
        { "StartDate", DateTime.Now }
    });

    var person2 = new DynamicNode(["DynamicPerson", "Intern"], new Dictionary<string, object?>
    {
        { "Name", "Dynamic Eve" },
        { "Role", "Intern" },
        { "StartDate", DateTime.Now }
    });

    await graph.CreateNodeAsync(person1);
    await graph.CreateNodeAsync(person2);
    Console.WriteLine($"✓ Created dynamic node: {person1.Properties["Name"]} with role {person1.Properties["Role"]}");
    Console.WriteLine($"✓ Created dynamic node: {person2.Properties["Name"]} with role {person2.Properties["Role"]}");

    var managesRel = new DynamicRelationship("MANAGES", new Dictionary<string, object?>
    {
        { "Since", new DateTime(2023, 1, 1) },
        { "Notes", "Promising intern" }
    });

    await graph.CreateRelationshipAsync(
        graph.DynamicNodes().OfLabel("Manager"),
        managesRel,
        graph.DynamicNodes().OfLabel("Intern"));
    Console.WriteLine($"✓ Created dynamic relationship: {person1.Properties["Name"]} MANAGES {person2.Properties["Name"]} since {managesRel.Properties["Since"]}\n");


    Console.WriteLine("\n=== Example 0 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Creating nodes with complex properties");
    Console.WriteLine("• Using C# 13 record types with the GraphModel");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("Make sure Neo4j is running on localhost:7687 with username 'neo4j' and password 'password'");
}
finally
{
    await store.DisposeAsync();
    await driver.DisposeAsync();
}

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

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 1: Basic CRUD Operations
// Demonstrates fundamental create, read, update, delete operations with nodes and relationships

Console.WriteLine("=== Example 0: Basic Serialization ===\n");

const string databaseName = "example0";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession())
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
    Console.WriteLine($"  Home Address: {charlie.HomeAddress.Street}, {charlie.HomeAddress.City}, {charlie.HomeAddress.State} {charlie.HomeAddress.ZipCode}, {charlie.HomeAddress.Country}");
    Console.WriteLine($"  Work Address: {charlie.WorkAddress.Street}, {charlie.WorkAddress.City}, {charlie.WorkAddress.State} {charlie.WorkAddress.ZipCode}, {charlie.WorkAddress.Country}\n");

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
        Console.WriteLine($"  Address: {address.Street}, {address.City}, {address.State} {address.ZipCode}, {address.Country}");
    }

    // Serialize a relationship
    Console.WriteLine("3. Creating a relationship between Alice and Bob...");
    var aliceFriendBob = new Friend(alice.Id, bob.Id) { Since = new DateTime(2021, 1, 1) };
    await graph.CreateRelationshipAsync(aliceFriendBob);

    Console.WriteLine($"✓ Created relationship between {alice.Name} and {bob.Name}");
    Console.WriteLine($"  Since: {aliceFriendBob.Since}");

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
    await graph.DisposeAsync();
    await using (var session = driver.AsyncSession())
    {
        //await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}
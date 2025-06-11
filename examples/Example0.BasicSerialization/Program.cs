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

var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, loggerFactory);
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
        Department = "Engineering"
    };

    var bob = new Person
    {
        Name = "Bob Smith",
        Email = "bob@techcorp.com",
        Age = 28,
        Department = "Marketing"
    };

    // Save nodes to graph
    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);

    Console.WriteLine($"✓ Created employee: {alice.Name}");
    Console.WriteLine($"✓ Created employee: {bob.Name}\n");

    // Create a node with complex a complex property
    var charlie = new PersonWithAddress
    {
        Name = "Charlie Brown",
        Email = "charlie@techcorp.com",
        Age = 35,
        Department = "Design",
        Address = new Address
        {
            Street = "456 Elm St",
            City = "Springfield",
            State = "IL",
            ZipCode = "62704",
            Country = "USA"
        }
    };

    // Save complex node to graph
    await graph.CreateNodeAsync(charlie);
    Console.WriteLine($"✓ Created employee with address: {charlie.Name}");
    Console.WriteLine($"  Address: {charlie.Address.Street}, {charlie.Address.City}, {charlie.Address.State} {charlie.Address.ZipCode}, {charlie.Address.Country}\n");

    // Create a node with a list of complex properties
    var dave = new PersonWithListOfAddresses
    {
        Name = "Dave Wilson",
        Email = "dave@techcorp.com",
        Age = 40,
        Department = "Sales",
        Addresses = new List<Address>
        {
            new Address
            {
                Street = "789 Oak St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62704",
                Country = "USA"
            },
            new Address
            {
                Street = "101 Pine St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62704",
                Country = "USA"
            }
        }
    };

    // Save the node with a list of addresses
    Console.WriteLine("2. Creating employee with multiple addresses...");
    await graph.CreateNodeAsync(dave);
    Console.WriteLine($"✓ Created employee with multiple addresses: {dave.Name}");
    foreach (var address in dave.Addresses)
    {
        Console.WriteLine($"  Address: {address.Street}, {address.City}, {address.State} {address.ZipCode}, {address.Country}");
    }


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
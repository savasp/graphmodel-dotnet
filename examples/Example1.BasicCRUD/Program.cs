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
using Neo4j.Driver;

// Example 1: Basic CRUD Operations
// Demonstrates fundamental create, read, update, delete operations with nodes and relationships

Console.WriteLine("=== Example 1: Basic CRUD Operations ===\n");

const string databaseName = "example1";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession())
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
}

Console.WriteLine($"✓ Created database: {databaseName}");

// We start with the Neo4j Graph Provider here

// Create graph instance with Neo4j provider
var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null);
var graph = store.Graph;

try
{
    // ==== CREATE OPERATIONS ====
    Console.WriteLine("1. Creating nodes...");

    // Create company
    var techCorp = new Company
    {
        Name = "TechCorp",
        Industry = "Technology",
        Founded = new DateTime(2010, 1, 1)
    };

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
    await graph.CreateNodeAsync(techCorp);
    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);

    Console.WriteLine($"✓ Created company: {techCorp.Name}");
    Console.WriteLine($"✓ Created employee: {alice.Name}");
    Console.WriteLine($"✓ Created employee: {bob.Name}\n");

    // Create relationships
    Console.WriteLine("2. Creating relationships...");

    var aliceWorksFor = new WorksFor(alice.Id, techCorp.Id, isBidirectional: false)
    {
        Position = "Senior Software Engineer",
        StartDate = new DateTime(2022, 3, 15),
        Salary = 95000
    };

    var bobWorksFor = new WorksFor(bob.Id, techCorp.Id, isBidirectional: false)
    {
        Position = "Marketing Manager",
        StartDate = new DateTime(2021, 8, 1),
        Salary = 75000
    };

    await graph.CreateRelationshipAsync(aliceWorksFor);
    await graph.CreateRelationshipAsync(bobWorksFor);

    Console.WriteLine($"✓ Created relationship: {alice.Name} works for {techCorp.Name}");
    Console.WriteLine($"✓ Created relationship: {bob.Name} works for {techCorp.Name}\n");

    // ==== READ OPERATIONS ====
    Console.WriteLine("3. Reading data...");

    // Find all people
    var allPeople = graph.Nodes<Person>().ToList();
    Console.WriteLine($"Found {allPeople.Count} people:");
    foreach (var person in allPeople)
    {
        Console.WriteLine($"  - {person.Name} ({person.Email})");
    }

    // Find specific person by name
    var foundAlice = graph.Nodes<Person>()
        .Where(p => p.Name == "Alice Johnson")
        .FirstOrDefault();

    if (foundAlice != null)
    {
        Console.WriteLine($"\nFound Alice: {foundAlice.Name}, Age: {foundAlice.Age}");
    }

    // Find company
    var foundCompany = graph.Nodes<Company>()
        .Where(c => c.Name == "TechCorp")
        .FirstOrDefault();

    if (foundCompany != null)
    {
        Console.WriteLine($"Found company: {foundCompany.Name}, Industry: {foundCompany.Industry}");
    }

    // Find relationships
    var paths = graph.Nodes<Person>()
        .PathSegments<WorksFor, Company>()
        .ToList();
    Console.WriteLine($"\nFound {paths.Count} work relationships:");
    foreach (var path in paths)
    {
        Console.WriteLine($"  - {path.StartNode.Name} works as {path.Relationship.Position} and {path.EndNode.Name} (Salary: ${path.Relationship.Salary:N0})");
    }

    // ==== UPDATE OPERATIONS ====
    Console.WriteLine("\n4. Updating data...");

    // Update Alice's age and department
    if (foundAlice != null)
    {
        foundAlice.Age = 31; // Update age
        foundAlice.Department = "Engineering"; // Update department
        await graph.UpdateNodeAsync(foundAlice);
        Console.WriteLine($"✓ Updated Alice's age to {foundAlice.Age} and department to {foundAlice.Department}");
    }

    // Update Bob's salary
    var bobRelationship = graph.Nodes<Person>()
        .PathSegments<WorksFor, Company>()
        .Where(r => r.StartNode.Name == "Bob Smith")
        .Select(r => r.Relationship)
        .FirstOrDefault();

    if (bobRelationship != null)
    {
        bobRelationship.Salary = 80000; // Update salary
        await graph.UpdateRelationshipAsync(bobRelationship);
        Console.WriteLine($"✓ Updated Bob's salary to ${bobRelationship.Salary:N0}");
    }

    // ==== VERIFY UPDATES ====
    Console.WriteLine("\n5. Verifying updates...");

    var updatedAlice = graph.Nodes<Person>()
        .Where(p => p.Name == "Alice Johnson")
        .FirstOrDefault();

    if (updatedAlice != null)
    {
        Console.WriteLine($"Alice's updated info: Age {updatedAlice.Age}, Department: {updatedAlice.Department}");
    }

    // ==== DELETE OPERATIONS ====
    Console.WriteLine("\n6. Demonstrating delete operations...");

    // Create a temporary person to delete
    var tempPerson = new Person
    {
        Name = "Temporary Employee",
        Email = "temp@techcorp.com",
        Age = 25,
        Department = "Temp"
    };

    await graph.CreateNodeAsync(tempPerson);
    Console.WriteLine($"✓ Created temporary employee: {tempPerson.Name}");

    // Delete the temporary person
    await graph.DeleteNodeAsync(tempPerson.Id);
    Console.WriteLine($"✓ Deleted temporary employee: {tempPerson.Name}");

    // Verify deletion
    var deletedPerson = graph.Nodes<Person>()
        .Where(p => p.Name == "Temporary Employee")
        .FirstOrDefault();

    Console.WriteLine(deletedPerson == null
        ? "✓ Confirmed: Temporary employee was successfully deleted"
        : "✗ Error: Temporary employee still exists");

    Console.WriteLine("\n=== Example 1 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Creating nodes and relationships");
    Console.WriteLine("• Reading data with LINQ queries");
    Console.WriteLine("• Updating existing nodes and relationships");
    Console.WriteLine("• Deleting nodes");
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
        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}
// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 1: Basic CRUD Operations
// Demonstrates fundamental create, read, update, delete operations with nodes and relationships

Console.WriteLine("=== Example 1: Basic CRUD Operations ===\n");

const string databaseName = "example1";

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

    var aliceWorksFor = new WorksFor
    {
        Position = "Senior Software Engineer",
        StartDate = new DateTime(2022, 3, 15),
        Salary = 95000
    };

    var bobWorksFor = new WorksFor
    {
        Position = "Marketing Manager",
        StartDate = new DateTime(2021, 8, 1),
        Salary = 75000
    };

    var companySelection = graph.Nodes<Company>().Where(company => company.Name == techCorp.Name);
    await graph.CreateRelationshipAsync(
        graph.Nodes<Person>().Where(person => person.Email == alice.Email),
        aliceWorksFor,
        companySelection);
    await graph.CreateRelationshipAsync(
        graph.Nodes<Person>().Where(person => person.Email == bob.Email),
        bobWorksFor,
        companySelection);

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
    var foundAlice = await graph.Nodes<Person>()
        .Where(p => p.Name == "Alice Johnson")
        .FirstOrDefaultAsync();

    if (foundAlice != null)
    {
        Console.WriteLine($"\nFound Alice: {foundAlice.Name}, Age: {foundAlice.Age}");
    }

    // Find company
    var foundCompany = await graph.Nodes<Company>()
        .Where(c => c.Name == "TechCorp")
        .FirstOrDefaultAsync();

    if (foundCompany != null)
    {
        Console.WriteLine($"Found company: {foundCompany.Name}, Industry: {foundCompany.Industry}");
    }

    // Find relationships
    var paths = await graph.Nodes<Person>()
        .PathSegments<Person, WorksFor, Company>()
        .ToListAsync();
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
        await graph.Nodes<Person>()
            .Where(person => person.Email == foundAlice.Email)
            .UpdateAsync(setters => setters
                .SetProperty(person => person.Age, 31)
                .SetProperty(person => person.Department, "Engineering"));
        Console.WriteLine("✓ Updated Alice's age to 31 and department to Engineering");
    }

    // Update Bob's salary
    var bobRelationship = graph.Nodes<Person>()
        .PathSegments<Person, WorksFor, Company>()
        .Where(r => r.StartNode.Name == "Bob Smith")
        .Select(r => r.Relationship);

    if (await bobRelationship.AnyAsync())
    {
        await bobRelationship.UpdateAsync(
            setters => setters.SetProperty(relationship => relationship.Salary, 80000));
        Console.WriteLine("✓ Updated Bob's salary to $80,000");
    }

    // ==== VERIFY UPDATES ====
    Console.WriteLine("\n5. Verifying updates...");

    var updatedAlice = await graph.Nodes<Person>()
        .Where(p => p.Name == "Alice Johnson")
        .FirstOrDefaultAsync();

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
    await graph.Nodes<Person>()
        .Where(person => person.Email == tempPerson.Email)
        .DeleteAsync();
    Console.WriteLine($"✓ Deleted temporary employee: {tempPerson.Name}");

    // Verify deletion
    var deletedPerson = await graph.Nodes<Person>()
        .Where(p => p.Name == "Temporary Employee")
        .FirstOrDefaultAsync();

    Console.WriteLine(deletedPerson == null
        ? "✓ Confirmed: Temporary employee was successfully deleted"
        : "✗ Error: Temporary employee still exists");

    Console.WriteLine("\n=== Example 1 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Creating nodes and relationships");
    Console.WriteLine("• Reading data with LINQ queries");
    Console.WriteLine("• Updating existing nodes and relationships");
    Console.WriteLine("• Deleting nodes");
    Console.WriteLine("• Using C# 14 record types with CVOYA Graph");
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

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
using Cvoya.Graph.Model.Neo4j.Linq;
using Neo4j.Driver;

// Example 2: LINQ and Traversal
// Demonstrates LINQ querying and graph traversal with depth control

Console.WriteLine("=== Example 2: LINQ and Traversal ===\n");

const string databaseName = "example2";

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
    // ==== SETUP: Create a social network ====
    Console.WriteLine("1. Creating social network...");

    // Create people
    var alice = new Person { Name = "Alice", Age = 30, City = "New York" };
    var bob = new Person { Name = "Bob", Age = 28, City = "San Francisco" };
    var charlie = new Person { Name = "Charlie", Age = 35, City = "New York" };
    var diana = new Person { Name = "Diana", Age = 32, City = "San Francisco" };
    var eve = new Person { Name = "Eve", Age = 29, City = "New York" };

    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);
    await graph.CreateNodeAsync(charlie);
    await graph.CreateNodeAsync(diana);
    await graph.CreateNodeAsync(eve);

    // Create relationships
    await graph.CreateRelationshipAsync(new Knows(alice.Id, bob.Id) { Since = DateTime.UtcNow.AddYears(-5) });
    await graph.CreateRelationshipAsync(new Knows(alice.Id, charlie.Id) { Since = DateTime.UtcNow.AddYears(-3) });
    await graph.CreateRelationshipAsync(new Knows(bob.Id, diana.Id) { Since = DateTime.UtcNow.AddYears(-2) });
    await graph.CreateRelationshipAsync(new Knows(charlie.Id, eve.Id) { Since = DateTime.UtcNow.AddYears(-1) });
    await graph.CreateRelationshipAsync(new Knows(diana.Id, eve.Id) { Since = DateTime.UtcNow.AddMonths(-6) });

    Console.WriteLine("✓ Created 5 people and their relationships\n");

    // ==== LINQ QUERIES ====
    Console.WriteLine("2. LINQ Query Examples...");

    // Basic filtering
    var newYorkers = graph.Nodes<Person>()
        .Where(p => p.City == "New York")
        .OrderBy(p => p.Name)
        .ToList();

    Console.WriteLine($"People in New York ({newYorkers.Count}):");
    foreach (var person in newYorkers)
    {
        Console.WriteLine($"  - {person.Name}, Age: {person.Age}");
    }

    // Age range query
    var youngPeople = graph.Nodes<Person>()
        .Where(p => p.Age >= 28 && p.Age <= 32)
        .ToList();

    Console.WriteLine($"\nPeople aged 28-32 ({youngPeople.Count}):");
    foreach (var person in youngPeople)
    {
        Console.WriteLine($"  - {person.Name}, Age: {person.Age}");
    }

    // ==== TRAVERSAL WITH DEPTH CONTROL ====
    Console.WriteLine("\n3. Graph Traversal Examples...");

    // Depth 0: Just the node
    alice = await graph.GetNodeAsync<Person>(alice.Id);
    Console.WriteLine($"\nAlice with depth 0:");
    Console.WriteLine($"  - Name: {alice.Name}");

    // Depth 1: Node with immediate relationships
    var aliceKnowsDepth1 = await graph.Nodes<Person>()
        .Where(p => p.Id == alice.Id)
        .Traverse<Person, Knows, Person>()
        .ToListAsync();

    Console.WriteLine($"\nAlice with depth 1:");
    Console.WriteLine($"  - Name: {alice.Name}");
    Console.WriteLine($"  - Knows: {string.Join(", ", aliceKnowsDepth1.Select(k => k.Name))}");

    // Depth 2: Two levels of relationships
    var aliceKnowsDepth2 = await graph.Nodes<Person>()
        .Where(p => p.Id == alice.Id)
        .Traverse<Person, Knows, Person>()
        .WithDepth(2)
        .ToListAsync();

    Console.WriteLine($"\nAlice with depth 2:");
    Console.WriteLine($"  - Name: {alice.Name}");
    foreach (var knows in aliceKnowsDepth2)
    {
        Console.WriteLine($"  - Knows: {knows.Name}");
    }

    // ==== COMPLEX LINQ QUERIES ====
    Console.WriteLine("\n4. Complex LINQ Queries...");

    // Find people who know someone in San Francisco
    var peopleWhoKnowSomeoneInSF = await graph.Nodes<Person>()
        .PathSegments<Person, Knows, Person>()
        .Where(p => p.EndNode.City == "San Francisco")
        .Select(p => p.StartNode)
        .Distinct()
        .ToListAsync();

    Console.WriteLine($"\nPeople who know someone in San Francisco:");
    foreach (var person in peopleWhoKnowSomeoneInSF)
    {
        Console.WriteLine($"  - {person.Name} (Age: {person.Age}, City: {person.City})");
    }

    // ==== FINDING MUTUAL CONNECTIONS ====
    Console.WriteLine("\n5. Finding mutual connections...");

    // Method 1: Using PathSegments to find mutual connections more efficiently
    var allPeople = await graph.Nodes<Person>().ToListAsync();

    // For each pair of people, find their mutual connections
    var mutualConnections = new List<(Person person1, Person person2, List<Person> mutual)>();

    foreach (var person1 in allPeople)
    {
        foreach (var person2 in allPeople.Where(p => p.Id != person1.Id && string.Compare(p.Id, person1.Id) > 0))
        {
            // Get people that person1 knows
            var person1Knows = await graph.Nodes<Person>()
                .Where(p => p.Id == person1.Id)
                .Traverse<Person, Knows, Person>()
                .ToListAsync();

            // Get people that person2 knows
            var person2Knows = await graph.Nodes<Person>()
                .Where(p => p.Id == person2.Id)
                .Traverse<Person, Knows, Person>()
                .ToListAsync();

            // Find mutual connections
            var mutual = person1Knows
                .Where(p1k => person2Knows.Any(p2k => p2k.Id == p1k.Id))
                .ToList();

            if (mutual.Any())
            {
                mutualConnections.Add((person1, person2, mutual));
            }
        }
    }

    // Display results
    Console.WriteLine($"\nMutual connections found:");
    foreach (var (person1, person2, mutual) in mutualConnections)
    {
        var mutualNames = string.Join(", ", mutual.Select(m => m.Name));
        Console.WriteLine($"  - {person1.Name} and {person2.Name} both know: {mutualNames}");
    }

    // Method 2: More efficient approach using a single query per person pair
    Console.WriteLine("\n\nAlternative approach - Finding mutual connections for specific pairs:");

    // Let's find mutual connections between Alice and Bob specifically

    // Get Alice's connections
    var aliceConnections = await graph.Nodes<Person>()
        .Where(p => p.Id == alice.Id)
        .Traverse<Person, Knows, Person>()
        .Select(p => p.Id)
        .ToListAsync();

    // Find Bob's connections that are also in Alice's connections
    var mutualBetweenAliceAndBob = await graph.Nodes<Person>()
        .Where(p => p.Id == bob.Id)
        .Traverse<Person, Knows, Person>()
        .Where(p => aliceConnections.Contains(p.Id))
        .ToListAsync();

    if (mutualBetweenAliceAndBob.Any())
    {
        Console.WriteLine($"  - Alice and Bob both know: {string.Join(", ", mutualBetweenAliceAndBob.Select(m => m.Name))}");
    }
    else
    {
        Console.WriteLine($"  - Alice and Bob have no mutual connections");
    }

    // Method 3: Using PathSegments for more complex analysis
    Console.WriteLine("\n\nFinding people with the most mutual connections:");

    // Get all connections as path segments
    var allConnections = await graph.Nodes<Person>()
        .PathSegments<Person, Knows, Person>()
        .ToListAsync();

    // Group by person to get their connections
    var connectionsByPerson = allConnections
        .GroupBy(path => path.StartNode.Id)
        .ToDictionary(
            g => g.Key,
            g => g.Select(path => path.EndNode.Id).ToHashSet()
        );

    // Calculate mutual connection counts for each pair
    var mutualCounts = new List<(string person1, string person2, int count)>();

    foreach (var person1 in allPeople)
    {
        foreach (var person2 in allPeople.Where(p => string.Compare(p.Id, person1.Id) > 0))
        {
            if (connectionsByPerson.TryGetValue(person1.Id, out var p1Connections) &&
                connectionsByPerson.TryGetValue(person2.Id, out var p2Connections))
            {
                var mutualCount = p1Connections.Intersect(p2Connections).Count();
                if (mutualCount > 0)
                {
                    mutualCounts.Add((person1.Name, person2.Name, mutualCount));
                }
            }
        }
    }

    // Show the pair with the most mutual connections
    var topPair = mutualCounts.OrderByDescending(mc => mc.count).FirstOrDefault();
    if (topPair != default)
    {
        Console.WriteLine($"  - {topPair.person1} and {topPair.person2} have the most mutual connections: {topPair.count}");
    }

    Console.WriteLine("\n=== Example 2 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• LINQ queries with filtering, ordering, and projection");
    Console.WriteLine("• Graph traversal with depth control");
    Console.WriteLine("• Complex queries involving relationships");
    Console.WriteLine("• Finding patterns in the graph");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        Console.WriteLine(ex.InnerException.StackTrace);
    }
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

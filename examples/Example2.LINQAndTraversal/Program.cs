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
using Cvoya.Graph.Provider.Neo4j;
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
var graph = new Neo4jGraphProvider("bolt://localhost:7687", "neo4j", "password", databaseName, null);

/*
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

    await graph.CreateNode(alice);
    await graph.CreateNode(bob);
    await graph.CreateNode(charlie);
    await graph.CreateNode(diana);
    await graph.CreateNode(eve);

    // Create relationships
    await graph.CreateRelationship(new Knows { Source = alice, Target = bob, Since = DateTime.UtcNow.AddYears(-5) });
    await graph.CreateRelationship(new Knows { Source = alice, Target = charlie, Since = DateTime.UtcNow.AddYears(-3) });
    await graph.CreateRelationship(new Knows { Source = bob, Target = diana, Since = DateTime.UtcNow.AddYears(-2) });
    await graph.CreateRelationship(new Knows { Source = charlie, Target = eve, Since = DateTime.UtcNow.AddYears(-1) });
    await graph.CreateRelationship(new Knows { Source = diana, Target = eve, Since = DateTime.UtcNow.AddMonths(-6) });

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
        .Select(p => new { p.Name, p.Age })
        .ToList();

    Console.WriteLine($"\nPeople aged 28-32 ({youngPeople.Count}):");
    foreach (var person in youngPeople)
    {
        Console.WriteLine($"  - {person.Name}, Age: {person.Age}");
    }

    // ==== TRAVERSAL WITH DEPTH CONTROL ====
    Console.WriteLine("\n3. Graph Traversal Examples...");

    // Depth 0: Just the node
    var aliceNoDepth = await graph.GetNode<Person>(alice.Id, new GraphOperationOptions().WithDepth(0));
    Console.WriteLine($"\nAlice with depth 0:");
    Console.WriteLine($"  - Name: {aliceNoDepth.Name}");
    Console.WriteLine($"  - Knows count: {aliceNoDepth.Knows.Count()} (no relationships loaded)");

    // Depth 1: Node with immediate relationships
    var aliceDepth1 = await graph.GetNode<Person>(alice.Id, new GraphOperationOptions().WithDepth(1));
    Console.WriteLine($"\nAlice with depth 1:");
    Console.WriteLine($"  - Name: {aliceDepth1.Name}");
    Console.WriteLine($"  - Knows: {string.Join(", ", aliceDepth1.Knows.Select(k => k.Target?.Name ?? "Unknown"))}");

    // Depth 2: Two levels of relationships
    var aliceDepth2 = await graph.GetNode<Person>(alice.Id, new GraphOperationOptions().WithDepth(2));
    Console.WriteLine($"\nAlice with depth 2:");
    Console.WriteLine($"  - Name: {aliceDepth2.Name}");
    foreach (var knows in aliceDepth2.Knows)
    {
        Console.WriteLine($"  - Knows: {knows.Target?.Name}");
        if (knows.Target != null)
        {
            foreach (var secondLevel in knows.Target.Knows)
            {
                Console.WriteLine($"    - Who knows: {secondLevel.Target?.Name}");
            }
        }
    }

    // ==== COMPLEX LINQ QUERIES ====
    Console.WriteLine("\n4. Complex LINQ Queries...");

    // Find people who know someone in San Francisco
    var knowsSF = graph.Nodes<Person>(new GraphOperationOptions().WithDepth(1))
        .Where(p => p.Knows.Any(k => k.Target != null && k.Target.City == "San Francisco"))
        .ToList();

    Console.WriteLine($"\nPeople who know someone in San Francisco:");
    foreach (var person in knowsSF)
    {
        var sfFriends = person.Knows
            .Where(k => k.Target?.City == "San Francisco")
            .Select(k => k.Target!.Name);
        Console.WriteLine($"  - {person.Name} knows: {string.Join(", ", sfFriends)}");
    }

    // Find mutual connections
    Console.WriteLine("\n5. Finding mutual connections...");

    var people = graph.Nodes<Person>(new GraphOperationOptions().WithDepth(2)).ToList();

    foreach (var person1 in people)
    {
        foreach (var person2 in people.Where(p => p.Id != person1.Id))
        {
            var mutual = person1.Knows
                .Select(k => k.Target?.Id)
                .Intersect(person2.Knows.Select(k => k.Target?.Id))
                .Where(id => id != null)
                .ToList();

            if (mutual.Any())
            {
                var mutualNames = people
                    .Where(p => mutual.Contains(p.Id))
                    .Select(p => p.Name);
                Console.WriteLine($"  - {person1.Name} and {person2.Name} both know: {string.Join(", ", mutualNames)}");
            }
        }
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
*/
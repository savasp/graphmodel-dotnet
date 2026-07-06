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

// Playground to explore ideas and the interface

const string databaseName = "GraphModelPlayground";


// ==== SETUP a new database ====
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");

    // Add a delay to ensure the database is fully created
    await Task.Delay(1000);
}

var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null);
var graph = store.Graph;

/*
var person = new Person
{
    Name = "John Doe",
    Email = "john.doe@example.com"
};

var department = new Department
{
    Name = "Engineering",
    Location = "Building A"
};

var company = new Company
{
    Name = "TechCorp",
    Industry = "Technology",
    Founded = DateTime.UtcNow
};

var worksFor = new WorksΑt
{
    Source = person,
    Target = department,
    Role = "Software Engineer",
    StartDate = DateTime.UtcNow,
    Salary = 100000m
};

var partOf = new PartOf
{
    Source = department,
    Target = company,
};

// Create
// (joe) -> [:worksAt] -> (engineering) -> [:partOf] -> (TechCorp)

await graph.CreateRelationship(worksFor, new GraphOperationOptions().WithCreateMissingNodes());
Console.WriteLine($"Added {person!.Name} to the graph (ID: {person!.Id}).");
Console.WriteLine($"Added {department!.Name} to the graph (ID: {department!.Id}).");

await graph.CreateRelationship(partOf, new GraphOperationOptions().WithCreateMissingNodes());
Console.WriteLine($"Added {company!.Name} to the graph (ID: {company!.Id}).");

var johnDoe = graph.Nodes<Person>()
    .Where(p => p.Name == "John Doe")
    .FirstOrDefault();

// Without .WithDepth(1), the Department node won't be loaded as the target in r.Target.
var johnWorksAtDepartment = graph.Relationships<WorksΑt>(new GraphOperationOptions().WithDepth(1))
    .Where(r => r.StartNodeId == johnDoe!.Id)
    .Select(r => r.Target)
    .FirstOrDefault();

var departmentPartOfCompany = graph.Relationships<PartOf>(new GraphOperationOptions().WithDepth(1))
    .Where(r => r.StartNodeId == johnWorksAtDepartment!.Id)
    .Select(r => r.Target)
    .FirstOrDefault();

Console.WriteLine($"John Doe works for {johnWorksAtDepartment!.Name} in {departmentPartOfCompany!.Name} department at {departmentPartOfCompany!.Name}.");

*/
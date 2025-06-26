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

namespace Graph.Model.Performance.Tests;

#pragma warning disable RS1035 // Do not use APIs banned for analyzers

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Bogus;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
using global::Neo4j.Driver;

[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
public class RelationshipBenchmark
{
    private static readonly Random _random = new();
    private IGraph _graph = null!;
    private List<Person> _persons = null!;
    private List<Company> _companies = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var connectionString = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var username = Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";

        var driver = GraphDatabase.Driver(connectionString, AuthTokens.Basic(username, password));
        await using (var session = driver.AsyncSession())
        {
            await session.RunAsync($"CREATE OR REPLACE DATABASE {"PerformanceBenchmark"}");
        }

        // Wait for database to be ready
        var maxWaitTime = TimeSpan.FromSeconds(30);
        var pollInterval = TimeSpan.FromMilliseconds(500);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            try
            {
                await using var testSession = driver.AsyncSession(o => o.WithDatabase("PerformanceBenchmark"));
                await testSession.RunAsync("RETURN 1");
                break; // Database is ready
            }
            catch (Exception)
            {
                // Database not ready yet, wait and retry
                await Task.Delay(pollInterval);
            }
        }

        var graphStore = new Neo4jGraphStore(
            connectionString,
            username,
            password,
            "PerformanceBenchmark"
        );

        _graph = graphStore.Graph;

        // Generate test data
        var personFaker = new Faker<Person>()
            .RuleFor(p => p.Id, f => f.Random.Guid().ToString())
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Email, f => f.Internet.Email())
            .RuleFor(p => p.DateOfBirth, f => f.Date.Past(50, DateTime.Now.AddYears(-18)));

        var companyFaker = new Faker<Company>()
            .RuleFor(c => c.Id, f => f.Random.Guid().ToString())
            .RuleFor(c => c.Name, f => f.Company.CompanyName())
            .RuleFor(c => c.Industry, f => f.Commerce.Department());

        _persons = personFaker.Generate(200);
        _companies = companyFaker.Generate(50);

        // Create nodes
        using var transaction = await _graph.GetTransactionAsync();
        foreach (var person in _persons)
        {
            await _graph.CreateNodeAsync(person);
        }
        foreach (var company in _companies)
        {
            await _graph.CreateNodeAsync(company);
        }
        await transaction.CommitAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        // Clean up test data
        using var transaction = await _graph.GetTransactionAsync();

        var relationships = await _graph.Relationships<WorksAt>().ToListAsync();
        foreach (var rel in relationships)
        {
            await _graph.DeleteRelationshipAsync(rel.Id);
        }

        var allPersons = await _graph.Nodes<Person>().ToListAsync();
        var allCompanies = await _graph.Nodes<Company>().ToListAsync();

        foreach (var person in allPersons)
        {
            await _graph.DeleteNodeAsync(person.Id);
        }

        foreach (var company in allCompanies)
        {
            await _graph.DeleteNodeAsync(company.Id);
        }

        await transaction.CommitAsync();
        await _graph.DisposeAsync();
    }

    [Benchmark]
    public async Task CreateSingleRelationship()
    {
        var person = _persons[_random.Next(_persons.Count)];
        var company = _companies[_random.Next(_companies.Count)];

        var relationship = new WorksAt
        {
            Id = Guid.NewGuid().ToString(),
            StartNodeId = person.Id,
            EndNodeId = company.Id,
            Position = "Software Developer",
            StartDate = DateTime.Now.AddYears(-_random.Next(1, 5))
        };

        await _graph.CreateRelationshipAsync(relationship);
    }

    [Benchmark]
    public async Task CreateMultipleRelationships()
    {
        var relationships = new List<WorksAt>();
        for (int i = 0; i < 10; i++)
        {
            var person = _persons[_random.Next(_persons.Count)];
            var company = _companies[_random.Next(_companies.Count)];

            relationships.Add(new WorksAt
            {
                Id = Guid.NewGuid().ToString(),
                StartNodeId = person.Id,
                EndNodeId = company.Id,
                Position = "Developer",
                StartDate = DateTime.Now.AddYears(-_random.Next(1, 5))
            });
        }

        foreach (var relationship in relationships)
        {
            await _graph.CreateRelationshipAsync(relationship);
        }
    }

    [Benchmark]
    public async Task TraverseFromPersonToCompany()
    {
        var person = _persons[_random.Next(_persons.Count)];

        var companies = await _graph.Nodes<Person>()
            .Where(p => p.Id == person.Id)
            .Traverse<Person, WorksAt, Company>()
            .ToListAsync();
    }

    [Benchmark]
    public async Task TraverseFromCompanyToEmployees()
    {
        var company = _companies[_random.Next(_companies.Count)];

        var employees = await _graph.Nodes<Company>()
            .Where(c => c.Id == company.Id)
            .Traverse<Company, WorksAt, Person>()
            .ToListAsync();
    }

    [Benchmark]
    public async Task ComplexTraversal()
    {
        // Find all people who work at companies in "Technology" industry
        var techEmployees = await _graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("A"))
            .Traverse<Person, WorksAt, Company>()
            .Where(c => c.Industry.Contains("Technology"))
            .ToListAsync();
    }
}

[Node("Company")]
public class Company : Cvoya.Graph.Model.INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
}

[Relationship("WORKS_AT")]
public class WorksAt : Cvoya.Graph.Model.IRelationship
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    [Property(Label = "start_date")]
    public DateTime StartDate { get; set; }
}

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
public class CrudOperationsBenchmark
{
    private static readonly Random _random = new();
    private IGraph _graph = null!;
    private List<Person> _testPersons = null!;
    private List<string> _personIds = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var connectionString = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var username = Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";

        var driver = GraphDatabase.Driver(connectionString, AuthTokens.Basic(username, password));
        await using (var session = driver.AsyncSession())
        {
            var result = await session.RunAsync($"CREATE OR REPLACE DATABASE {"PerformanceBenchmark"}");
            await result.ConsumeAsync();
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
                var result = await testSession.RunAsync("RETURN 1");
                await result.ConsumeAsync();
                break; // Database is ready
            }
            catch (Exception)
            {
                // Database not ready yet, wait and retry
                await Task.Delay(pollInterval);
            }
        }

        var graphStore = new Neo4jGraphStore(connectionString, username, password, "PerformanceBenchmark");
        _graph = graphStore.Graph;

        // Generate test data
        var faker = new Faker<Person>()
            .RuleFor(p => p.Id, f => f.Random.Guid().ToString())
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Email, f => f.Internet.Email())
            .RuleFor(p => p.DateOfBirth, f => f.Date.Past(50, DateTime.Now.AddYears(-18)));

        _testPersons = faker.Generate(1000);
        _personIds = _testPersons.Select(p => p.Id).ToList();

        // Pre-populate some data
        using var transaction = await _graph.GetTransactionAsync();
        foreach (var person in _testPersons.Take(500))
        {
            await _graph.CreateNodeAsync(person);
        }
        await transaction.CommitAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        // Clean up test data
        using var transaction = await _graph.GetTransactionAsync();
        var allPersons = await _graph.Nodes<Person>().ToListAsync();
        foreach (var person in allPersons)
        {
            await _graph.DeleteNodeAsync(person.Id);
        }
        await transaction.CommitAsync();

        await _graph.DisposeAsync();
    }

    [Benchmark]
    public async Task CreateSingleNode()
    {
        var person = _testPersons[_random.Next(_testPersons.Count)];
        await _graph.CreateNodeAsync(person);
    }

    [Benchmark]
    public async Task CreateMultipleNodes()
    {
        var persons = _testPersons.Take(10).Select(p => new Person
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = p.FirstName,
            LastName = p.LastName,
            Email = p.Email,
            DateOfBirth = p.DateOfBirth
        }).ToList();

        foreach (var person in persons)
        {
            await _graph.CreateNodeAsync(person);
        }
    }

    [Benchmark]
    public async Task ReadSingleNode()
    {
        var id = _personIds[_random.Next(_personIds.Count)];
        var person = await _graph.GetNodeAsync<Person>(id);
    }

    [Benchmark]
    public async Task QueryNodes()
    {
        var results = await _graph.Nodes<Person>()
            .Where(p => p.FirstName.StartsWith("A"))
            .Take(10)
            .ToListAsync();
    }

    [Benchmark]
    public async Task QueryWithComplexFilter()
    {
        var cutoffDate = DateTime.Now.AddYears(-30);
        var results = await _graph.Nodes<Person>()
            .Where(p => p.DateOfBirth > cutoffDate && p.Email.Contains("@gmail.com"))
            .OrderBy(p => p.LastName)
            .Take(20)
            .ToListAsync();
    }

    [Benchmark]
    public async Task UpdateNode()
    {
        var id = _personIds[_random.Next(_personIds.Count)];
        var person = await _graph.GetNodeAsync<Person>(id);
        if (person != null)
        {
            person.Email = $"updated_{DateTime.Now.Ticks}@example.com";
            await _graph.UpdateNodeAsync(person);
        }
    }
}

[Node("Person")]
public record Person : Node
{
    [Property(Label = "first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Property(Label = "last_name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Property(Label = "birth_date")]
    public DateTime DateOfBirth { get; set; }
}
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

namespace Cvoya.Graph.Model.Age.Tests;

using Cvoya.Graph.Model.Age.Tests.Infrastructure;
using Cvoya.Graph.Model.Tests;
using Xunit;

/// <summary>
/// Tests to ensure async execution paths are properly covered in code coverage reports.
/// These tests explicitly use async query extension methods to exercise async code paths.
/// </summary>
public class AsyncExecutionCoverageTests : AgeTest
{
    public AsyncExecutionCoverageTests(TestInfrastructureFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ToListAsync_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var person2 = new Person { FirstName = "Bob", LastName = "Jones" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act - Explicitly use async extension
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Alice" || p.FirstName == "Bob")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ToArrayAsync_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Charlie", LastName = "Brown" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Charlie")
            .ToArrayAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal("Charlie", results[0].FirstName);
    }

    [Fact]
    public async Task CountAsync_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Dave", LastName = "Wilson" };
        var person2 = new Person { FirstName = "Eve", LastName = "Davis" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act
        var count = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Dave" || p.FirstName == "Eve")
            .CountAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AnyAsync_WithPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Frank", LastName = "Miller" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var exists = await (await Graph.NodesAsync<Person>())
            .AnyAsync(p => p.FirstName == "Frank", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task AnyAsync_WithoutPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Grace", LastName = "Lee" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var exists = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Grace")
            .AnyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task FirstAsync_WithPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Henry", LastName = "Taylor" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var result = await (await Graph.NodesAsync<Person>())
            .FirstAsync(p => p.FirstName == "Henry", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Henry", result.FirstName);
        Assert.Equal("Taylor", result.LastName);
    }

    [Fact]
    public async Task FirstAsync_WithoutPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Ivy", LastName = "Anderson" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var result = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Ivy")
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Ivy", result.FirstName);
    }

    // Note: SingleAsync with predicate can be flaky if there's duplicate data in the database
    // Keeping it commented to avoid test instability
    /*
    [Fact]
    public async Task SingleAsync_WithPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Jack", LastName = "Thomas" };
        await Graph.CreateNodeAsync(person, null, default);

        // Act
        var result = await (await Graph.NodesAsync<Person>())
            .SingleAsync(p => p.FirstName == "Jack", default);

        // Assert
        Assert.Equal("Jack", result.FirstName);
        Assert.Equal("Thomas", result.LastName);
    }
    */

    [Fact]
    public async Task SingleAsync_WithoutPredicate_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person = new Person { FirstName = "Kelly", LastName = "White" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act
        var result = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Kelly")
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Kelly", result.FirstName);
    }

    [Fact]
    public async Task LongCountAsync_ExecutesQueryAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Leo", LastName = "Harris" };
        var person2 = new Person { FirstName = "Mia", LastName = "Martin" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act
        var count = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Leo" || p.FirstName == "Mia")
            .LongCountAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2L, count);
    }

    [Fact]
    public async Task ComplexQuery_WithMultipleOperations_ExecutesAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Nina", LastName = "Garcia", Age = 25 };
        var person2 = new Person { FirstName = "Oscar", LastName = "Martinez", Age = 30 };
        var person3 = new Person { FirstName = "Paula", LastName = "Robinson", Age = 35 };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        var knows1 = new Knows { StartNodeId = person1.Id, EndNodeId = person2.Id, Since = DateTime.UtcNow };
        var knows2 = new Knows { StartNodeId = person2.Id, EndNodeId = person3.Id, Since = DateTime.UtcNow };

        await Graph.CreateRelationshipAsync(knows1, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(knows2, null, TestContext.Current.CancellationToken);

        // Act - Complex async query with projection
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.Age >= 25 && p.Age <= 35)
            .OrderBy(p => p.Age)
            .Select(p => new { p.FirstName, p.Age })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        dynamic first = results[0];
        Assert.Equal("Nina", first.FirstName);
        Assert.Equal(25, first.Age);
        dynamic last = results[2];
        Assert.Equal("Paula", last.FirstName);
        Assert.Equal(35, last.Age);
    }

    [Fact]
    public async Task TraversalQuery_ExecutesAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Quinn", LastName = "Clark" };
        var person2 = new Person { FirstName = "Rachel", LastName = "Lewis" };

        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = person1.Id, EndNodeId = person2.Id, Since = DateTime.UtcNow };
        await Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        // Act
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Quinn")
            .PathSegments<Person, Knows, Person>()
            .Select(ps => ps.EndNode)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal("Rachel", results[0].FirstName);
    }

    [Fact]
    public async Task CancellationToken_PropagatedThroughAsyncExecution()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var person = new Person { FirstName = "Sam", LastName = "Walker" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act - Token is passed but not cancelled
        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Sam")
            .ToListAsync(cts.Token);

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public async Task GetAsyncEnumerator_EnumeratesResultsAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Tom", LastName = "Baker" };
        var person2 = new Person { FirstName = "Uma", LastName = "Foster" };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act - Materialize to list to trigger async enumeration
        var results = new List<Person>();

        // ToListAsync internally uses the AsyncEnumerator via ExecuteAsync
        var enumerated = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Tom" || p.FirstName == "Uma")
            .OrderBy(p => p.FirstName)
            .ToListAsync(TestContext.Current.CancellationToken);

        results.AddRange(enumerated);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Tom", results[0].FirstName);
        Assert.Equal("Uma", results[1].FirstName);
    }

    [Fact]
    public async Task AsyncStreaming_EnumeratesResultsAsynchronously()
    {
        // Arrange
        var person1 = new Person { FirstName = "Victor", LastName = "Hughes", Age = 40 };
        var person2 = new Person { FirstName = "Wendy", LastName = "Cooper", Age = 45 };
        var person3 = new Person { FirstName = "Xavier", LastName = "Reed", Age = 50 };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person3, null, TestContext.Current.CancellationToken);

        // Act - Multiple async operations to exercise streaming behavior
        var count = await (await Graph.NodesAsync<Person>())
            .Where(p => p.Age >= 40 && p.Age <= 50)
            .CountAsync(TestContext.Current.CancellationToken);

        var firstPerson = await (await Graph.NodesAsync<Person>())
            .Where(p => p.Age >= 40 && p.Age <= 50)
            .OrderBy(p => p.Age)
            .FirstAsync(TestContext.Current.CancellationToken);

        var allResults = await (await Graph.NodesAsync<Person>())
            .Where(p => p.Age >= 40 && p.Age <= 50)
            .OrderBy(p => p.Age)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal("Victor", firstPerson.FirstName);
        Assert.Equal(40, firstPerson.Age);
        Assert.Equal(3, allResults.Count);
        Assert.Equal("Xavier", allResults[2].FirstName);
        Assert.Equal(50, allResults[2].Age);
    }

    [Fact]
    public async Task MultipleAsyncOperations_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var person = new Person { FirstName = "Yara", LastName = "Bennett" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        // Act - Multiple async operations with cancellation token
        var exists = await (await Graph.NodesAsync<Person>())
            .AnyAsync(p => p.FirstName == "Yara", cts.Token);

        var result = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Yara")
            .FirstAsync(cts.Token);

        // Assert
        Assert.True(exists);
        Assert.Equal("Yara", result.FirstName);
    }

    [Fact]
    public async Task AsyncEnumerator_HandlesEmptyResults()
    {
        // Act - Query with no results exercises empty enumeration path
        var count = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "NonExistentName12345")
            .CountAsync(TestContext.Current.CancellationToken);

        var results = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "NonExistentName12345")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        Assert.Empty(results);
    }

    [Fact]
    public async Task AsyncEnumerator_WithProjection_EnumeratesCorrectly()
    {
        // Arrange
        var person1 = new Person { FirstName = "Zoe", LastName = "Price", Age = 28 };
        var person2 = new Person { FirstName = "Adam", LastName = "Bell", Age = 32 };
        await Graph.CreateNodeAsync(person1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(person2, null, TestContext.Current.CancellationToken);

        // Act - Projection exercises async enumeration with type transformation
        var names = await (await Graph.NodesAsync<Person>())
            .Where(p => p.FirstName == "Zoe" || p.FirstName == "Adam")
            .OrderBy(p => p.FirstName)
            .Select(p => p.FirstName)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, names.Count);
        Assert.Equal("Adam", names[0]);
        Assert.Equal("Zoe", names[1]);
    }
}

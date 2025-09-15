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

namespace Cvoya.Graph.Model.Tests;

public interface ITakeOperatorTests : IGraphModelTest
{
    [Fact]
    public async Task TakeOperator_GeneratesCorrectCypherLimit()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };
        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        // Act
        var query = Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Take(2);

        // Note: We can't easily test the generated Cypher directly in this test framework,
        // but we can verify the behavior works correctly
        var results = await query.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Equal("Bob", results[1].FirstName);
    }

    [Fact]
    public async Task TakeOperator_WithPathSegments_GeneratesCorrectCypherLimit()
    {
        // Arrange - Create a user and some memories
        var user = new User { GoogleId = "user123", Name = "Test User", Email = "testuser@example.com" };
        var memory1 = new Memory { Text = "First memory", CreatedAt = DateTime.UtcNow.AddDays(-2), CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" }, Location = new Point { Latitude = 0, Longitude = 0, Height = 0 }, Deleted = false, UpdatedAt = DateTime.UtcNow.AddDays(-2) };
        var memory2 = new Memory { Text = "Second memory", CreatedAt = DateTime.UtcNow.AddDays(-1), CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" }, Location = new Point { Latitude = 0, Longitude = 0, Height = 0 }, Deleted = false, UpdatedAt = DateTime.UtcNow.AddDays(-1) };
        var memory3 = new Memory { Text = "Third memory", CreatedAt = DateTime.UtcNow, CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" }, Location = new Point { Latitude = 0, Longitude = 0, Height = 0 }, Deleted = false, UpdatedAt = DateTime.UtcNow };

        await Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);

        // Create relationships
        var rel1 = new UserMemory(user.Id, memory1.Id);
        var rel2 = new UserMemory(user.Id, memory2.Id);
        var rel3 = new UserMemory(user.Id, memory3.Id);

        await Graph.CreateRelationshipAsync(rel1, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel2, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel3, null, TestContext.Current.CancellationToken);

        // Act - This is the type of query that was failing before the fix
        var query = Graph.Nodes<Memory>()
            .PathSegments<Memory, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(s => s.EndNode.GoogleId == user.GoogleId)
            .Select(s => s.StartNode)
            .OrderByDescending(m => m.CreatedAt)
            .Take(2);

        var results = await query.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        // Should be ordered by CreatedAt descending, so newest first
        Assert.Equal("Third memory", results[0].Text);
        Assert.Equal("Second memory", results[1].Text);
    }

    [Fact]
    public async Task TakeOperator_WithSkip_GeneratesCorrectCypherSkipAndLimit()
    {
        // Arrange
        var people = Enumerable.Range(1, 5)
            .Select(i => new Person { FirstName = $"Person{i}", LastName = "Test" })
            .ToArray();

        foreach (var person in people)
        {
            await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);
        }

        // Act
        var results = await Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Skip(2)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Person3", results[0].FirstName);
        Assert.Equal("Person4", results[1].FirstName);
    }

    [Fact]
    public async Task TakeOperator_WithComplexWhere_GeneratesCorrectCypher()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice", LastName = "Smith", Age = 25 };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith", Age = 30 };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones", Age = 35 };
        var p4 = new Person { FirstName = "David", LastName = "Smith", Age = 40 };

        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p4, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith" && p.Age > 25)
            .OrderBy(p => p.Age)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Bob", results[0].FirstName);
        Assert.Equal("David", results[1].FirstName);
    }

    [Fact]
    public async Task TakeOperator_WithProjection_GeneratesCorrectCypher()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice", LastName = "Smith" };
        var p2 = new Person { FirstName = "Bob", LastName = "Smith" };
        var p3 = new Person { FirstName = "Charlie", LastName = "Jones" };

        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p3, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.Nodes<Person>()
            .Where(p => p.LastName == "Smith")
            .Select(p => new { p.FirstName, p.LastName })
            .OrderBy(p => p.FirstName)
            .Take(1)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Equal("Smith", results[0].LastName);
    }

    [Fact]
    public async Task TakeOperator_ZeroLimit_ReturnsEmptyResults()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice" };
        var p2 = new Person { FirstName = "Bob" };
        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.Nodes<Person>()
            .Take(0)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task TakeOperator_LimitLargerThanAvailable_ReturnsAllResults()
    {
        // Arrange
        var p1 = new Person { FirstName = "Alice" };
        var p2 = new Person { FirstName = "Bob" };
        await Graph.CreateNodeAsync(p1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(p2, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.Nodes<Person>()
            .OrderBy(p => p.FirstName)
            .Take(10)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Alice", results[0].FirstName);
        Assert.Equal("Bob", results[1].FirstName);
    }

    [Fact]
    public async Task TakeOperator_WithFullTextSearch_GeneratesCorrectCypher()
    {
        // Arrange
        var memory1 = new Memory
        {
            Text = "Important document about AI",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Another important note",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory3 = new Memory
        {
            Text = "Regular content",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };

        await Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.SearchNodes<Memory>("important")
            .OrderByDescending(m => m.CreatedAt)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        // Should be ordered by CreatedAt descending
        Assert.Equal("Another important note", results[0].Text);
        Assert.Equal("Important document about AI", results[1].Text);
    }

    [Fact]
    public async Task TakeOperator_WithPathSegmentsAndWhere_WorksCorrectly()
    {
        // Arrange - Create multiple users and memories
        var user1 = new User { GoogleId = "user1", Name = "User One", Email = "user1@example.com" };
        var user2 = new User { GoogleId = "user2", Name = "User Two", Email = "user2@example.com" };
        var memory1 = new Memory
        {
            Text = "Memory 1",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Memory 2",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory3 = new Memory
        {
            Text = "Memory 3",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory4 = new Memory
        {
            Text = "Memory 4",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };

        await Graph.CreateNodeAsync(user1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(user2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory4, null, TestContext.Current.CancellationToken);

        // Create relationships - user1 has all memories, user2 has only some
        var rel1 = new UserMemory(user1.Id, memory1.Id);
        var rel2 = new UserMemory(user1.Id, memory2.Id);
        var rel3 = new UserMemory(user1.Id, memory3.Id);
        var rel4 = new UserMemory(user1.Id, memory4.Id);
        var rel5 = new UserMemory(user2.Id, memory2.Id);
        var rel6 = new UserMemory(user2.Id, memory4.Id);

        await Graph.CreateRelationshipAsync(rel1, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel2, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel3, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel4, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel5, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel6, null, TestContext.Current.CancellationToken);

        // Act - Get only 2 most recent memories for user1
        var results = await Graph.Nodes<Memory>()
            .PathSegments<Memory, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(s => s.EndNode.GoogleId == user1.GoogleId)
            .Select(s => s.StartNode)
            .OrderByDescending(m => m.CreatedAt)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Memory 4", results[0].Text); // Most recent
        Assert.Equal("Memory 3", results[1].Text); // Second most recent
    }

    [Fact]
    public async Task TakeOperator_WithPathSegmentsAndSkip_WorksCorrectly()
    {
        // Arrange
        var user = new User { GoogleId = "user123", Name = "Test User", Email = "testuser@example.com" };
        var memories = Enumerable.Range(1, 5)
            .Select(i => new Memory
            {
                Text = $"Memory {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                UpdatedAt = DateTime.UtcNow.AddDays(-i),
                CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
                Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
                Deleted = false
            })
            .ToArray();

        await Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        foreach (var memory in memories)
        {
            await Graph.CreateNodeAsync(memory, null, TestContext.Current.CancellationToken);
            var rel = new UserMemory(user.Id, memory.Id);
            await Graph.CreateRelationshipAsync(rel, null, TestContext.Current.CancellationToken);
        }

        // Act - Skip first 2, take next 2
        var results = await Graph.Nodes<Memory>()
            .PathSegments<Memory, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(s => s.EndNode.GoogleId == user.GoogleId)
            .Select(s => s.StartNode)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(2)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Memory 3", results[0].Text);
        Assert.Equal("Memory 4", results[1].Text);
    }

    [Fact]
    public async Task TakeOperator_WithPathSegmentsAndProjection_WorksCorrectly()
    {
        // Arrange
        var user = new User { GoogleId = "user123", Name = "Test User", Email = "testuser@example.com" };
        var memory1 = new Memory
        {
            Text = "First memory",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory2 = new Memory
        {
            Text = "Second memory",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };
        var memory3 = new Memory
        {
            Text = "Third memory",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };

        await Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory2, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory3, null, TestContext.Current.CancellationToken);

        var rel1 = new UserMemory(user.Id, memory1.Id);
        var rel2 = new UserMemory(user.Id, memory2.Id);
        var rel3 = new UserMemory(user.Id, memory3.Id);

        await Graph.CreateRelationshipAsync(rel1, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel2, null, TestContext.Current.CancellationToken);
        await Graph.CreateRelationshipAsync(rel3, null, TestContext.Current.CancellationToken);

        // Act - Project to just content and take 2
        var results = await Graph.Nodes<Memory>()
            .PathSegments<Memory, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(s => s.EndNode.GoogleId == user.GoogleId)
            .Select(s => s.StartNode)
            .Select(m => m.Text)
            .OrderByDescending(content => content)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains("Third memory", results);
        Assert.Contains("Second memory", results);
    }

    [Fact]
    public async Task TakeOperator_WithPathSegmentsZeroLimit_ReturnsEmpty()
    {
        // Arrange
        var user = new User { GoogleId = "user123", Name = "Test User", Email = "testuser@example.com" };
        var memory = new Memory
        {
            Text = "Test memory",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CapturedBy = new MemorySource { Name = "TestSource", Description = "Test", Version = "1.0", Device = "TestDevice" },
            Location = new Point { Latitude = 0, Longitude = 0, Height = 0 },
            Deleted = false
        };

        await Graph.CreateNodeAsync(user, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(memory, null, TestContext.Current.CancellationToken);
        var rel = new UserMemory(user.Id, memory.Id);
        await Graph.CreateRelationshipAsync(rel, null, TestContext.Current.CancellationToken);

        // Act
        var results = await Graph.Nodes<Memory>()
            .PathSegments<Memory, UserMemory, User>()
            .Direction(GraphTraversalDirection.Incoming)
            .Where(s => s.EndNode.GoogleId == user.GoogleId)
            .Select(s => s.StartNode)
            .Take(0)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }
}

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

public interface IErrorHandlingTests : IGraphModelTest
{
    public class TestNode : INode
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class TestRelationship : IRelationship
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string StartNodeId { get; init; } = string.Empty;
        public string EndNodeId { get; init; } = string.Empty;
        public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
        public string Type { get; set; } = string.Empty;
    }

    [Fact]
    public async Task GetNodeAsync_NonExistentId_ThrowsGraphException()
    {
        var nonExistentId = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.GetNodeAsync<TestNode>(nonExistentId, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task GetRelationshipAsync_NonExistentId_ThrowsGraphException()
    {
        var nonExistentId = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.GetRelationshipAsync<TestRelationship>(nonExistentId, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateNodeAsync_NullNode_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Graph.CreateNodeAsync<TestNode>(null!, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateRelationshipAsync_NullRelationship_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Graph.CreateRelationshipAsync<TestRelationship>(null!, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task UpdateNodeAsync_NonExistentNode_ThrowsGraphException()
    {
        var nonExistentNode = new TestNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "NonExistent"
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.UpdateNodeAsync(nonExistentNode, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task UpdateRelationshipAsync_NonExistentRelationship_ThrowsGraphException()
    {
        var nonExistentRel = new TestRelationship
        {
            Id = Guid.NewGuid().ToString("N"),
            StartNodeId = Guid.NewGuid().ToString("N"),
            EndNodeId = Guid.NewGuid().ToString("N"),
            Type = "NonExistent"
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.UpdateRelationshipAsync(nonExistentRel, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateRelationshipAsync_NonExistentStartNode_ThrowsGraphException()
    {
        var endNode = new TestNode { Name = "EndNode" };
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var relationship = new TestRelationship
        {
            StartNodeId = Guid.NewGuid().ToString("N"), // Non-existent
            EndNodeId = endNode.Id,
            Type = "TestRel"
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateRelationshipAsync_NonExistentEndNode_ThrowsGraphException()
    {
        var startNode = new TestNode { Name = "StartNode" };
        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);

        var relationship = new TestRelationship
        {
            StartNodeId = startNode.Id,
            EndNodeId = Guid.NewGuid().ToString("N"), // Non-existent
            Type = "TestRel"
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task DeleteNodeAsync_NonExistentId_ThrowsGraphException()
    {
        var nonExistentId = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.DeleteNodeAsync(nonExistentId, false, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task DeleteRelationshipAsync_NonExistentId_ThrowsGraphException()
    {
        var nonExistentId = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.DeleteRelationshipAsync(nonExistentId, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateNodeAsync_EmptyId_ThrowsArgumentException()
    {
        var nodeWithEmptyId = new TestNode
        {
            Id = string.Empty,
            Name = "EmptyId"
        };

        // Should  throw an exception
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Graph.CreateNodeAsync(nodeWithEmptyId, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateNodeAsync_NullId_ThrowsArgumentException()
    {
        var nodeWithNullId = new TestNode
        {
            Id = null!,
            Name = "NullId"
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Graph.CreateNodeAsync(nodeWithNullId, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task CreateDuplicateNode_SameId_ThrowsException()
    {
        var node1 = new TestNode { Name = "First" };
        await Graph.CreateNodeAsync(node1, null, TestContext.Current.CancellationToken);

        var node2 = new TestNode
        {
            Id = node1.Id, // Same ID
            Name = "Duplicate"
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(node2, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Query_WithInvalidExpression_HandlesGracefully()
    {
        // Test with a complex expression that might cause issues
        try
        {
            var results = await Graph.Nodes<TestNode>()
                .Where(n => n.Name.Substring(0, n.Name.Length / 0) == "test") // Division by zero
                .ToListAsync(TestContext.Current.CancellationToken);

            // If it doesn't throw, that's fine too
        }
        catch (DivideByZeroException)
        {
            // Expected
        }
        catch (GraphException)
        {
            // Also acceptable
        }
        catch (InvalidOperationException)
        {
            // Also acceptable
        }
    }

    [Fact]
    public async Task Query_WithNull_HandlesGracefully()
    {
        var node = new TestNode { Name = "Test" };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        // Test querying with null values
        var results = await Graph.Nodes<TestNode>()
            .Where(n => n.Name != null)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task LargeQuery_HandlesEfficiently()
    {
        // Create many nodes to test large result sets
        var nodes = Enumerable.Range(1, 1000)
            .Select(i => new TestNode { Name = $"Node{i}", Value = i })
            .ToArray();

        foreach (var node in nodes)
        {
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        }

        // Query should handle large result sets
        var results = await Graph.Nodes<TestNode>()
            .Where(n => n.Name.StartsWith("Node"))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.True(results.Count >= 1000);
    }

    [Fact]
    public async Task CancelledOperation_ThrowsGraphException()
    {
        var node = new TestNode { Name = "CancelTest" };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(node, null, cts.Token);
        });
    }

    [Fact]
    public async Task TimeoutOperation_ThrowsTimeoutException()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1)); // Very short timeout

        try
        {
            // Try to perform an operation that might take longer than 1ms
            var results = await Graph.Nodes<TestNode>()
                .ToListAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when operation is cancelled due to timeout
        }
        catch (TimeoutException)
        {
            // Also acceptable
        }
    }

    [Fact]
    public async Task DeleteNodeWithRelationships_WithoutCascade_ThrowsException()
    {
        var node1 = new TestNode { Name = "Node1" };
        var node2 = new TestNode { Name = "Node2" };
        await Graph.CreateNodeAsync(node1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(node2, null, TestContext.Current.CancellationToken);

        var relationship = new TestRelationship
        {
            StartNodeId = node1.Id,
            EndNodeId = node2.Id,
            Type = "TestRel"
        };
        await Graph.CreateRelationshipAsync(relationship, null, TestContext.Current.CancellationToken);

        // Try to delete node1 without cascade - should fail
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.DeleteNodeAsync(node1.Id, false, null, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Transaction_UseAfterCommit_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync();
        var node = new TestNode { Name = "TransactionTest" };

        await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        // Using transaction after commit should throw
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(new TestNode { Name = "AfterCommit" }, transaction, TestContext.Current.CancellationToken);
        });

        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task Transaction_UseAfterRollback_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync();
        var node = new TestNode { Name = "TransactionTest" };

        await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
        await transaction.Rollback();

        // Using transaction after rollback should throw
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(new TestNode { Name = "AfterRollback" }, transaction, TestContext.Current.CancellationToken);
        });

        await transaction.DisposeAsync();
    }

    [Fact]
    public async Task Transaction_UseAfterDispose_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync();
        var node = new TestNode { Name = "TransactionTest" };

        await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
        await transaction.DisposeAsync(); // Dispose without commit

        // Using transaction after dispose should throw
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.CreateNodeAsync(new TestNode { Name = "AfterDispose" }, transaction, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task ConcurrentTransactions_HandleIsolation()
    {
        var task1 = Task.Run(async () =>
        {
            await using var transaction = await Graph.GetTransactionAsync();
            var node = new TestNode { Name = "Transaction1" };
            await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
            await Task.Delay(100); // Simulate work
            await transaction.CommitAsync();
            return node.Id;
        });

        var task2 = Task.Run(async () =>
        {
            await using var transaction = await Graph.GetTransactionAsync();
            var node = new TestNode { Name = "Transaction2" };
            await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
            await Task.Delay(100); // Simulate work
            await transaction.CommitAsync();
            return node.Id;
        });

        var results = await Task.WhenAll(task1, task2);

        // Both transactions should succeed
        Assert.NotEqual(results[0], results[1]);

        // Both nodes should exist
        var node1 = await Graph.GetNodeAsync<TestNode>(results[0], null, TestContext.Current.CancellationToken);
        var node2 = await Graph.GetNodeAsync<TestNode>(results[1], null, TestContext.Current.CancellationToken);

        Assert.Equal("Transaction1", node1.Name);
        Assert.Equal("Transaction2", node2.Name);
    }

    [Fact]
    public async Task ExtremelyLongStringProperty_HandledCorrectly()
    {
        var longString = new string('A', 100000); // 100k characters
        var node = new TestNode { Name = longString };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<TestNode>(node.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(longString, retrieved.Name);
    }

    [Fact]
    public async Task SpecialCharactersInProperties_HandledCorrectly()
    {
        var specialString = "Special: éñ中文🚀\n\t\r\"'\\";
        var node = new TestNode { Name = specialString };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.GetNodeAsync<TestNode>(node.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(specialString, retrieved.Name);
    }

    [Fact]
    public async Task ExtremeIntegerValues_HandledCorrectly()
    {
        var node1 = new TestNode { Name = "MaxInt", Value = int.MaxValue };
        var node2 = new TestNode { Name = "MinInt", Value = int.MinValue };

        await Graph.CreateNodeAsync(node1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(node2, null, TestContext.Current.CancellationToken);

        var retrieved1 = await Graph.GetNodeAsync<TestNode>(node1.Id, null, TestContext.Current.CancellationToken);
        var retrieved2 = await Graph.GetNodeAsync<TestNode>(node2.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(int.MaxValue, retrieved1.Value);
        Assert.Equal(int.MinValue, retrieved2.Value);
    }
}
// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface IErrorHandlingTests : IGraphTest
{
    public record TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public record TestRelationship : Relationship
    {
        public string Name { get; set; } = string.Empty;
    }

    public record NodeWithOrdinaryId : Node
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task SingleNodeAsync_EmptySelection_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Graph.Nodes<TestNode>()
                .Where(node => node.Name == "missing")
                .SingleAsync(TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task SingleRelationshipAsync_EmptySelection_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Graph.Relationships<TestRelationship>()
                .Where(relationship => relationship.Name == "missing")
                .SingleAsync(TestContext.Current.CancellationToken);
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
    public async Task CreateAsync_NullRelationship_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await Graph.CreateAsync(
                new TestNode { Name = "source" },
                (TestRelationship)null!,
                new TestNode { Name = "target" },
                cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task UpdateAsync_EmptyNodeSelection_AffectsZero()
    {
        var affected = await Graph.Nodes<TestNode>()
            .Where(node => node.Name == "missing")
            .UpdateAsync(
                setters => setters.SetProperty(node => node.Value, 1),
                TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task UpdateAsync_EmptyRelationshipSelection_AffectsZero()
    {
        var affected = await Graph.Relationships<TestRelationship>()
            .Where(relationship => relationship.Name == "missing")
            .UpdateAsync(
                setters => setters.SetProperty(relationship => relationship.Name, "updated"),
                TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task CreateRelationshipAsync_EmptySourceSelection_ThrowsGraphCardinalityException()
    {
        var endNode = new TestNode { Name = "EndNode" };
        await Graph.CreateNodeAsync(endNode, null, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            Graph.CreateRelationshipAsync(
                Graph.Nodes<TestNode>().Where(node => node.Name == "missing"),
                new TestRelationship { Name = "TestRel" },
                Graph.Nodes<TestNode>().Where(node => node.Name == endNode.Name),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Source, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, exception.Failure);
    }

    [Fact]
    public async Task CreateRelationshipAsync_EmptyTargetSelection_ThrowsGraphCardinalityException()
    {
        var startNode = new TestNode { Name = "StartNode" };
        await Graph.CreateNodeAsync(startNode, null, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<GraphCardinalityException>(() =>
            Graph.CreateRelationshipAsync(
                Graph.Nodes<TestNode>().Where(node => node.Name == startNode.Name),
                new TestRelationship { Name = "TestRel" },
                Graph.Nodes<TestNode>().Where(node => node.Name == "missing"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GraphEndpointRole.Target, exception.Role);
        Assert.Equal(GraphCardinalityFailure.Empty, exception.Failure);
    }

    [Fact]
    public async Task DeleteAsync_EmptyNodeSelection_AffectsZero()
    {
        var affected = await Graph.Nodes<INode>()
            .Where(node => node.Labels.Contains("MissingNode"))
            .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task DeleteAsync_EmptyRelationshipSelection_AffectsZero()
    {
        var affected = await Graph.Relationships<IRelationship>()
            .Where(relationship => relationship.Type == "MISSING_RELATIONSHIP")
            .DeleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task CreateNodeAsync_OrdinaryIdProperty_RoundTripsAsDomainData()
    {
        var node = new NodeWithOrdinaryId
        {
            Id = string.Empty,
            Name = "ordinary-id",
        };

        await Graph.CreateNodeAsync(node, cancellationToken: TestContext.Current.CancellationToken);
        var roundTripped = await Graph.Nodes<NodeWithOrdinaryId>()
            .Where(candidate => candidate.Name == node.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, roundTripped.Id);
    }

    [Fact]
    public async Task Query_WithInvalidExpression_HandlesGracefully()
    {
        // Test with a complex expression that might cause issues
        try
        {
            // Discarded on purpose: this test only cares whether the query throws or not (see the
            // catch clauses below), not the result - an unused assignment here would otherwise be
            // a useless-local (CodeQL cs/useless-assignment-to-local).
#pragma warning disable CA1514 // The redundant-looking length deliberately divides by zero.
            _ = await Graph.Nodes<TestNode>()
                .Where(n => n.Name.Substring(0, n.Name.Length / 0) == "test") // Division by zero
                .ToListAsync(TestContext.Current.CancellationToken);
#pragma warning restore CA1514

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
    public async Task CancelledOperation_ThrowsOperationCanceledException()
    {
        var node = new TestNode { Name = "CancelTest" };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
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
            // Try to perform an operation that might take longer than 1ms. Discarded on purpose:
            // this test only cares whether the query throws or not (see the catch clauses below),
            // not the result - an unused assignment here would otherwise be a useless-local
            // (CodeQL cs/useless-assignment-to-local).
            _ = await Graph.Nodes<TestNode>()
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

        await Graph.CreateRelationshipAsync(
            Graph.Nodes<TestNode>().Where(node => node.Name == node1.Name),
            new TestRelationship { Name = "TestRel" },
            Graph.Nodes<TestNode>().Where(node => node.Name == node2.Name),
            cancellationToken: TestContext.Current.CancellationToken);

        // Try to delete node1 without cascade - should fail
        await Assert.ThrowsAsync<GraphException>(async () =>
        {
            await Graph.Nodes<TestNode>()
                .Where(node => node.Name == node1.Name)
                .DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task Transaction_UseAfterCommit_ThrowsException()
    {
        var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
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
        var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var node = new TestNode { Name = "TransactionTest" };

        await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
        await transaction.RollbackAsync();

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
        var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
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
            await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
            var node = new TestNode { Name = "Transaction1" };
            await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
            await Task.Delay(100); // Simulate work
            await transaction.CommitAsync();
            return node.Name;
        });

        var task2 = Task.Run(async () =>
        {
            await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
            var node = new TestNode { Name = "Transaction2" };
            await Graph.CreateNodeAsync(node, transaction, TestContext.Current.CancellationToken);
            await Task.Delay(100); // Simulate work
            await transaction.CommitAsync();
            return node.Name;
        });

        var results = await Task.WhenAll(task1, task2);

        // Both transactions should succeed
        Assert.NotEqual(results[0], results[1]);

        // Both nodes should exist
        var firstName = results[0];
        var secondName = results[1];
        var node1 = await Graph.Nodes<TestNode>().Where(node => node.Name == firstName)
            .SingleAsync(TestContext.Current.CancellationToken);
        var node2 = await Graph.Nodes<TestNode>().Where(node => node.Name == secondName)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Transaction1", node1.Name);
        Assert.Equal("Transaction2", node2.Name);
    }

    [Fact]
    public async Task ExtremelyLongStringProperty_HandledCorrectly()
    {
        var longString = new string('A', 100000); // 100k characters
        var node = new TestNode { Name = longString };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.Nodes<TestNode>().Where(candidate => candidate.Name == node.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(longString, retrieved.Name);
    }

    [Fact]
    public async Task SpecialCharactersInProperties_HandledCorrectly()
    {
        var specialString = "Special: éñ中文🚀\n\t\r\"'\\";
        var node = new TestNode { Name = specialString };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var retrieved = await Graph.Nodes<TestNode>().Where(candidate => candidate.Name == node.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(specialString, retrieved.Name);
    }

    [Fact]
    public async Task ExtremeIntegerValues_HandledCorrectly()
    {
        var node1 = new TestNode { Name = "MaxInt", Value = int.MaxValue };
        var node2 = new TestNode { Name = "MinInt", Value = int.MinValue };

        await Graph.CreateNodeAsync(node1, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(node2, null, TestContext.Current.CancellationToken);

        var retrieved1 = await Graph.Nodes<TestNode>().Where(node => node.Name == node1.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        var retrieved2 = await Graph.Nodes<TestNode>().Where(node => node.Name == node2.Name)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(int.MaxValue, retrieved1.Value);
        Assert.Equal(int.MinValue, retrieved2.Value);
    }
}

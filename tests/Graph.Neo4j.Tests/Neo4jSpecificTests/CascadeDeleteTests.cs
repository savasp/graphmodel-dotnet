// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;


public class CascadeDeleteTests(Neo4jHarness harness) :
    Neo4jTest(harness)
{
    [Fact]
    public async Task DeleteAsync_RemovesFullComplexPropertySubtree()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var propertyValues = new[]
        {
            $"cascade-a-{suffix}",
            $"cascade-c-{suffix}",
            $"cascade-b-{suffix}"
        };

        var node = new Class1
        {
            Property1 = $"root-{suffix}",
            A = new ComplexClassA
            {
                Property1 = propertyValues[0],
                C = new ComplexClassC
                {
                    Property1 = propertyValues[1],
                    B = new ComplexClassB { Property1 = propertyValues[2] }
                }
            }
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var propertyNodeCount = await CountComplexPropertyNodesAsync(propertyValues);
        Assert.Equal(3, propertyNodeCount);

        await Graph.Nodes<Class1>().Where(candidate => candidate.Property1 == node.Property1)
            .DeleteAsync(cascadeDelete: true, cancellationToken: TestContext.Current.CancellationToken);

        var remainingPropertyNodeCount = await CountComplexPropertyNodesAsync(propertyValues);
        Assert.Equal(0, remainingPropertyNodeCount);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotDeleteMatchingDomainDataUnderDifferentRawLabel()
    {
        var node = new Class1 { Property1 = $"root-{Guid.NewGuid():N}" };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        await CreateRawCollisionNodeAsync(node.Property1);

        await Graph.Nodes<Class1>().Where(candidate => candidate.Property1 == node.Property1)
            .DeleteAsync(cascadeDelete: true, cancellationToken: TestContext.Current.CancellationToken);

        var collisionCount = await CountRawCollisionNodesAsync(node.Property1);
        Assert.Equal(1, collisionCount);

        Assert.Null(await Graph.Nodes<Class1>().Where(candidate => candidate.Property1 == node.Property1)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    private async Task<int> CountComplexPropertyNodesAsync(IReadOnlyCollection<string> propertyValues)
    {
        const string cypher = @"
            MATCH (propertyNode)
            WHERE any(label IN labels(propertyNode) WHERE label IN $propertyLabels)
                AND propertyNode.Property1 IN $propertyValues
            RETURN COUNT(propertyNode) AS count";

        return await ReadCountAsync(cypher, new
        {
            propertyLabels = new[] { nameof(ComplexClassA), nameof(ComplexClassB), nameof(ComplexClassC) },
            propertyValues = propertyValues.ToArray()
        });
    }

    private async Task CreateRawCollisionNodeAsync(string marker)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = @"
            CREATE (:CascadeDeleteCollision {Property1: $marker, Name: $name})";

        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new
        {
            marker,
            name = "Untouched"
        });

        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<int> CountRawCollisionNodesAsync(string marker)
    {
        const string cypher = @"
            MATCH (collision:CascadeDeleteCollision {Property1: $marker})
            RETURN COUNT(collision) AS count";

        return await ReadCountAsync(cypher, new { marker });
    }

    private async Task<int> ReadCountAsync(string cypher, object parameters)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        var result = await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);

        return record["count"].As<int>();
    }
}

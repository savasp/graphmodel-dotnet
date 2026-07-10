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

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;


public class CascadeDeleteTests(Neo4jHarness harness) :
    Neo4jTest(harness)
{
    [Fact]
    public async Task DeleteNodeAsync_RemovesFullComplexPropertySubtree()
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

        await Graph.DeleteNodeAsync(node.Id, true, null, TestContext.Current.CancellationToken);

        var remainingPropertyNodeCount = await CountComplexPropertyNodesAsync(propertyValues);
        Assert.Equal(0, remainingPropertyNodeCount);
    }

    [Fact]
    public async Task DeleteNodeAsync_DoesNotDeleteSameIdNodeUnderDifferentRawLabel()
    {
        var node = new Class1 { Property1 = $"root-{Guid.NewGuid():N}" };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        await CreateRawCollisionNodeAsync(node.Id);

        await Graph.DeleteNodeAsync(node.Id, true, null, TestContext.Current.CancellationToken);

        var collisionCount = await CountRawCollisionNodesAsync(node.Id);
        Assert.Equal(1, collisionCount);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Graph.GetNodeAsync<Class1>(node.Id, null, TestContext.Current.CancellationToken));
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

    private async Task CreateRawCollisionNodeAsync(string nodeId)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        const string cypher = @"
            CREATE (:CascadeDeleteCollision {Id: $nodeId, Name: $name})";

        var result = await neo4jTransaction.Transaction.RunAsync(cypher, new
        {
            nodeId,
            name = "Untouched"
        });

        await result.ConsumeAsync();
        await transaction.CommitAsync();
    }

    private async Task<int> CountRawCollisionNodesAsync(string nodeId)
    {
        const string cypher = @"
            MATCH (collision:CascadeDeleteCollision {Id: $nodeId})
            RETURN COUNT(collision) AS count";

        return await ReadCountAsync(cypher, new { nodeId });
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

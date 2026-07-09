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

namespace Cvoya.Graph.Model.Neo4j.Tests;

using Cvoya.Graph.Model.Neo4j.Core;
using global::Neo4j.Driver;

public sealed class FirstClassComplexPropertyTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task AttributeOverride_RoundTripsAndPersistsSemanticRelationship()
    {
        var node = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = "Seattle", Street = "1st Ave" }
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);
        var fetched = await Graph.GetNodeAsync<ComplexPropertyOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(node.Address, fetched.Address);
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.Id }));
    }

    [Fact]
    public async Task SharedInMemoryValue_CreatesOneValueNodePerOwner()
    {
        var shared = new ComplexAddress { City = "Portland", Street = "Burnside" };
        var first = new ComplexPropertyOwner { Address = shared };
        var second = new ComplexPropertyOwner { Address = shared };

        await Graph.CreateNodeAsync(first, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(second, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, await CountAsync(
            "MATCH (owner:ComplexPropertyOwner)-[:PRIMARY_ADDRESS]->(address:ComplexAddress) " +
            "WHERE owner.Id IN $ids RETURN count(DISTINCT address) AS count",
            new { ids = new[] { first.Id, second.Id } }));
    }

    [Fact]
    public async Task SlimOwner_CanCoLoadRelatedValueThroughPathProjection()
    {
        var node = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = "Vancouver", Street = "Main" }
        };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var segment = await Graph.Nodes<Cvoya.Graph.Model.INode>()
            .Where(owner => owner.Id == node.Id)
            .PathSegments<Cvoya.Graph.Model.INode, PrimaryAddress, ComplexAddressNode>()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(node.Id, segment.StartNode.Id);
        Assert.Equal("Vancouver", segment.EndNode.City);
        Assert.Equal(segment.StartNode.Id, segment.Relationship.StartNodeId);
        Assert.Equal(segment.EndNode.Id, segment.Relationship.EndNodeId);
    }

    private async Task<int> CountAsync(string cypher, object parameters)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;
        var result = await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();
        return record["count"].As<int>();
    }
}

[Node("ComplexPropertyOwner")]
public sealed record ComplexPropertyOwner : Node
{
    [ComplexProperty(RelationshipType = "PRIMARY_ADDRESS")]
    public ComplexAddress Address { get; init; } = new();
}

public sealed record ComplexAddress
{
    public string Street { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;
}

[Node("ComplexAddress")]
public sealed record ComplexAddressNode : Node
{
    public string Street { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;
}

[Relationship("PRIMARY_ADDRESS")]
public sealed record PrimaryAddress(string StartNodeId, string EndNodeId)
    : Relationship(StartNodeId, EndNodeId);

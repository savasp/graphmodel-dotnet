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

    [Fact]
    public async Task UpdateNodeAsync_ComplexProperty_RoundTripsNewValue()
    {
        var node = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = "Seattle", Street = "1st Ave" }
        };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var replacement = new ComplexAddress { City = "Denver", Street = "16th St" };
        var updated = node with { Address = replacement };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.GetNodeAsync<ComplexPropertyOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(replacement, fetched.Address);
    }

    [Fact]
    public async Task UpdateNodeAsync_ComplexProperty_DeletesOrphanedValueNode()
    {
        var original = new ComplexAddress { City = "Seattle", Street = "1st Ave" };
        var replacement = new ComplexAddress { City = "Denver", Street = "16th St" };

        var node = new ComplexPropertyOwner { Address = original };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var updated = node with { Address = replacement };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.Id }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city AND a.Street = $street RETURN count(a) AS count",
            new { city = original.City, street = original.Street }));
    }

    [Fact]
    public async Task UpdateNodeAsync_ComplexPropertyCollection_ReplacesItemsInOrderAndDeletesOrphans()
    {
        var original = new List<ComplexAddress>
        {
            new() { City = "Seattle", Street = "1st Ave" },
            new() { City = "Portland", Street = "Burnside" },
            new() { City = "Vancouver", Street = "Main" }
        };

        var node = new ComplexPropertyCollectionOwner { Addresses = original };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var replacement = new List<ComplexAddress>
        {
            new() { City = "Denver", Street = "16th St" },
            new() { City = "Austin", Street = "Congress Ave" }
        };
        var updated = node with { Addresses = replacement };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.GetNodeAsync<ComplexPropertyCollectionOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(replacement, fetched.Addresses);

        Assert.Equal(2, await CountAsync(
            "MATCH (:ComplexPropertyCollectionOwner {Id: $id})-[r:Addresses]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.Id }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City IN $cities RETURN count(a) AS count",
            new { cities = original.Select(a => a.City).ToArray() }));
    }

    [Fact]
    public async Task UpdateNodeAsync_NestedComplexProperty_ReplacesEntireChainAndDeletesOrphansAtBothLevels()
    {
        var originalOffice = new ComplexOffice
        {
            Name = "HQ",
            Address = new ComplexAddress { City = "Seattle", Street = "1st Ave" }
        };

        var node = new NestedComplexPropertyOwner { Office = originalOffice };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var replacementOffice = new ComplexOffice
        {
            Name = "Branch",
            Address = new ComplexAddress { City = "Denver", Street = "16th St" }
        };
        var updated = node with { Office = replacementOffice };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.GetNodeAsync<NestedComplexPropertyOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(replacementOffice, fetched.Office);

        Assert.Equal(1, await CountAsync(
            "MATCH (:NestedComplexPropertyOwner {Id: $id})-[r1:Office]->(o:ComplexOffice)-[r2:Address]->(a:ComplexAddress) " +
            "WHERE r1.__graphModelComplexProperty = true AND r2.__graphModelComplexProperty = true " +
            "AND o.Name = $name AND a.City = $city RETURN count(a) AS count",
            new { id = node.Id, name = replacementOffice.Name, city = replacementOffice.Address.City }));

        Assert.Equal(0, await CountAsync(
            "MATCH (o:ComplexOffice) WHERE o.Name = $name RETURN count(o) AS count",
            new { name = originalOffice.Name }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city RETURN count(a) AS count",
            new { city = originalOffice.Address.City }));
    }

    [Fact]
    public async Task UpdateNodeAsync_ComplexProperty_DoesNotAffectOtherOwners()
    {
        var addressA = new ComplexAddress { City = "Seattle", Street = "1st Ave" };
        var addressB = new ComplexAddress { City = "Portland", Street = "Burnside" };

        var ownerA = new ComplexPropertyOwner { Address = addressA };
        var ownerB = new ComplexPropertyOwner { Address = addressB };
        await Graph.CreateNodeAsync(ownerA, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(ownerB, null, TestContext.Current.CancellationToken);

        var replacementA = new ComplexAddress { City = "Denver", Street = "16th St" };
        var updatedA = ownerA with { Address = replacementA };
        await Graph.UpdateNodeAsync(updatedA, null, TestContext.Current.CancellationToken);

        var fetchedB = await Graph.GetNodeAsync<ComplexPropertyOwner>(
            ownerB.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(addressB, fetchedB.Address);
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(a:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true AND a.City = $city AND a.Street = $street " +
            "RETURN count(a) AS count",
            new { id = ownerB.Id, city = addressB.City, street = addressB.Street }));
    }

    [Fact]
    public async Task ComplexPropertyCollection_EmptyRoundTrips_ThenUpdateAddsItems()
    {
        var node = new ComplexPropertyCollectionOwner { Addresses = [] };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetchedEmpty = await Graph.GetNodeAsync<ComplexPropertyCollectionOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.NotNull(fetchedEmpty.Addresses);
        Assert.Empty(fetchedEmpty.Addresses);

        var items = new List<ComplexAddress>
        {
            new() { City = "Seattle", Street = "1st Ave" },
            new() { City = "Tacoma", Street = "2nd Ave" }
        };
        var updated = node with { Addresses = items };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        var fetchedWithItems = await Graph.GetNodeAsync<ComplexPropertyCollectionOwner>(
            node.Id, null, TestContext.Current.CancellationToken);

        Assert.Equal(items, fetchedWithItems.Addresses);
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

[Node("ComplexPropertyCollectionOwner")]
public sealed record ComplexPropertyCollectionOwner : Node
{
    public List<ComplexAddress> Addresses { get; init; } = [];
}

public sealed record ComplexOffice
{
    public string Name { get; init; } = string.Empty;

    public ComplexAddress Address { get; init; } = new();
}

[Node("NestedComplexPropertyOwner")]
public sealed record NestedComplexPropertyOwner : Node
{
    public ComplexOffice Office { get; init; } = new();
}

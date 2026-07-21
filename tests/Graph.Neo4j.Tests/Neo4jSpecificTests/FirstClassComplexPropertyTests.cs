// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.Neo4j.Core;
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
        var fetched = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(node.Address, fetched.Address);
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {TestKey: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.TestKey }));
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
            "WHERE owner.TestKey IN $ids RETURN count(DISTINCT address) AS count",
            new { ids = new[] { first.TestKey, second.TestKey } }));
    }

    [Fact]
    public async Task MarkerOwnedValue_IsNotExposedThroughOrdinaryPathProjection()
    {
        var node = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = "Vancouver", Street = "Main" }
        };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var segment = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .PathSegments<ComplexPropertyOwner, PrimaryAddress, ComplexAddressNode>()
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.Null(segment);
    }

    [Fact]
    public async Task UpdateAsync_ComplexProperty_RoundTripsNewValue()
    {
        var node = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = "Seattle", Street = "1st Ave" }
        };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var replacement = new ComplexAddress { City = "Denver", Street = "16th St" };
        await Graph.Nodes<ComplexPropertyOwner>().Where(owner => owner.TestKey == node.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Address, replacement),
                TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(replacement, fetched.Address);
    }

    [Fact]
    public async Task UpdateAsync_ComplexProperty_DeletesOrphanedValueNode()
    {
        var original = new ComplexAddress { City = "Seattle", Street = "1st Ave" };
        var replacement = new ComplexAddress { City = "Denver", Street = "16th St" };

        var node = new ComplexPropertyOwner { Address = original };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        await Graph.Nodes<ComplexPropertyOwner>().Where(owner => owner.TestKey == node.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Address, replacement),
                TestContext.Current.CancellationToken);

        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {TestKey: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.TestKey }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city AND a.Street = $street RETURN count(a) AS count",
            new { city = original.City, street = original.Street }));
    }

    [Fact]
    public async Task UpdateAsync_ComplexPropertyCollection_ReplacesItemsInOrderAndDeletesOrphans()
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
        await Graph.Nodes<ComplexPropertyCollectionOwner>().Where(owner => owner.TestKey == node.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Addresses, replacement),
                TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<ComplexPropertyCollectionOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(replacement, fetched.Addresses);

        Assert.Equal(2, await CountAsync(
            "MATCH (:ComplexPropertyCollectionOwner {TestKey: $id})-[r:Addresses]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.TestKey }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City IN $cities RETURN count(a) AS count",
            new { cities = original.Select(a => a.City).ToArray() }));
    }

    [Fact]
    public async Task UpdateAsync_NestedComplexProperty_ReplacesEntireChainAndDeletesOrphansAtBothLevels()
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
        await Graph.Nodes<NestedComplexPropertyOwner>().Where(owner => owner.TestKey == node.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Office, replacementOffice),
                TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<NestedComplexPropertyOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(replacementOffice, fetched.Office);

        Assert.Equal(1, await CountAsync(
            "MATCH (:NestedComplexPropertyOwner {TestKey: $id})-[r1:Office]->(o:ComplexOffice)-[r2:Address]->(a:ComplexAddress) " +
            "WHERE r1.__graphModelComplexProperty = true AND r2.__graphModelComplexProperty = true " +
            "AND o.Name = $name AND a.City = $city RETURN count(a) AS count",
            new { id = node.TestKey, name = replacementOffice.Name, city = replacementOffice.Address.City }));

        Assert.Equal(0, await CountAsync(
            "MATCH (o:ComplexOffice) WHERE o.Name = $name RETURN count(o) AS count",
            new { name = originalOffice.Name }));

        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city RETURN count(a) AS count",
            new { city = originalOffice.Address.City }));
    }

    [Fact]
    public async Task UpdateAsync_ComplexProperty_DoesNotAffectOtherOwners()
    {
        var addressA = new ComplexAddress { City = "Seattle", Street = "1st Ave" };
        var addressB = new ComplexAddress { City = "Portland", Street = "Burnside" };

        var ownerA = new ComplexPropertyOwner { Address = addressA };
        var ownerB = new ComplexPropertyOwner { Address = addressB };
        await Graph.CreateNodeAsync(ownerA, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(ownerB, null, TestContext.Current.CancellationToken);

        var replacementA = new ComplexAddress { City = "Denver", Street = "16th St" };
        await Graph.Nodes<ComplexPropertyOwner>().Where(owner => owner.TestKey == ownerA.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Address, replacementA),
                TestContext.Current.CancellationToken);

        var fetchedB = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(owner => owner.TestKey == ownerB.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(addressB, fetchedB.Address);
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {TestKey: $id})-[r:PRIMARY_ADDRESS]->(a:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true AND a.City = $city AND a.Street = $street " +
            "RETURN count(a) AS count",
            new { id = ownerB.TestKey, city = addressB.City, street = addressB.Street }));
    }

    [Fact]
    public async Task ComplexPropertyCollection_LargeCollection_RoundTrips()
    {
        var offices = Enumerable.Range(0, 50)
            .Select(i => new ComplexOffice
            {
                Name = $"Office {i:D2}",
                Address = new ComplexAddress { City = $"City {i:D2}", Street = $"Street {i:D2}" }
            })
            .ToList();

        var node = new LargeComplexCollectionOwner { Offices = offices };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetched = await Graph.Nodes<LargeComplexCollectionOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(offices, fetched.Offices);

        Assert.Equal(50, await CountAsync(
            "MATCH (:LargeComplexCollectionOwner {TestKey: $id})-[r:Offices]->(:ComplexOffice) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.TestKey }));

        Assert.Equal(50, await CountAsync(
            "MATCH (:LargeComplexCollectionOwner {TestKey: $id})-[:Offices]->(:ComplexOffice)-[r:Address]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = node.TestKey }));
    }

    [Fact]
    public async Task ComplexPropertyCollection_EmptyRoundTrips_ThenUpdateAddsItems()
    {
        var node = new ComplexPropertyCollectionOwner { Addresses = [] };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var fetchedEmpty = await Graph.Nodes<ComplexPropertyCollectionOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(fetchedEmpty.Addresses);
        Assert.Empty(fetchedEmpty.Addresses);

        var items = new List<ComplexAddress>
        {
            new() { City = "Seattle", Street = "1st Ave" },
            new() { City = "Tacoma", Street = "2nd Ave" }
        };
        await Graph.Nodes<ComplexPropertyCollectionOwner>().Where(owner => owner.TestKey == node.TestKey)
            .UpdateAsync(
                setters => setters.SetProperty(owner => owner.Addresses, items),
                TestContext.Current.CancellationToken);

        var fetchedWithItems = await Graph.Nodes<ComplexPropertyCollectionOwner>()
            .Where(owner => owner.TestKey == node.TestKey)
            .SingleAsync(TestContext.Current.CancellationToken);

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
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

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
public sealed record PrimaryAddress : Relationship;

[Node("ComplexPropertyCollectionOwner")]
public sealed record ComplexPropertyCollectionOwner : Node
{
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

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
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

    public ComplexOffice Office { get; init; } = new();
}

[Node("LargeComplexCollectionOwner")]
public sealed record LargeComplexCollectionOwner : Node
{
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

    public List<ComplexOffice> Offices { get; init; } = [];
}

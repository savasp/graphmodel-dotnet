// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;

/// <summary>
/// Pins the collision semantics between user-declared domain relationships and complex-property
/// relationships that share a relationship type (#220). Complex-property edges are ordinary edges
/// by design, so the collision is legal:
/// <list type="bullet">
/// <item>cleanup (update replace, cascade delete) touches only marker-bearing edges/nodes — domain
/// data is never deleted by property cleanup;</item>
/// <item>materialization of complex properties filters on the marker — a colliding domain edge
/// never leaks into loaded property values;</item>
/// <item>LINQ navigation predicates over complex properties do not filter on the marker — a domain
/// edge whose type and target label match the pattern satisfies them.</item>
/// </list>
/// The tests reuse <see cref="ComplexPropertyOwner"/> (complex property with relationship type
/// <c>PRIMARY_ADDRESS</c> to a <c>:ComplexAddress</c> value node), <see cref="PrimaryAddress"/>
/// (domain relationship type <c>PRIMARY_ADDRESS</c>), and <see cref="ComplexAddressNode"/> (domain
/// node labeled <c>ComplexAddress</c>) — a full type + label collision.
/// </summary>
public sealed class DomainRelationshipCollisionTests(Neo4jHarness harness) : Neo4jTest(harness)
{
    [Fact]
    public async Task UpdateCleanup_RemovesOnlyMarkerBearingValueNode_DomainEdgeAndNodeSurvive()
    {
        var (owner, domainNode) = await SeedCollisionAsync(
            complexCity: "Complex Seattle", domainCity: "Domain Portland");

        var replacement = new ComplexAddress { City = "Complex Denver", Street = "16th St" };
        await Graph.UpdateNodeAsync(
            owner with { Address = replacement }, null, TestContext.Current.CancellationToken);

        // The domain edge and its target node survive property cleanup untouched.
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(a:ComplexAddress {Id: $domainId}) " +
            "WHERE r.__graphModelComplexProperty IS NULL RETURN count(r) AS count",
            new { id = owner.Id, domainId = domainNode.Id }));

        // Exactly one marker-bearing value node remains (the replacement); the original is gone.
        Assert.Equal(1, await CountAsync(
            "MATCH (:ComplexPropertyOwner {Id: $id})-[r:PRIMARY_ADDRESS]->(:ComplexAddress) " +
            "WHERE r.__graphModelComplexProperty = true RETURN count(r) AS count",
            new { id = owner.Id }));
        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city RETURN count(a) AS count",
            new { city = "Complex Seattle" }));

        // Materialization is marker-filtered: the domain node never leaks into the property value.
        var fetched = await Graph.GetNodeAsync<ComplexPropertyOwner>(
            owner.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(replacement, fetched.Address);
    }

    [Fact]
    public async Task CascadeDeleteCleanup_RemovesOnlyMarkerBearingValueNode_DomainNodeSurvives()
    {
        var (owner, domainNode) = await SeedCollisionAsync(
            complexCity: "Complex Vancouver", domainCity: "Domain Tacoma");

        await Graph.DeleteNodeAsync(owner.Id, true, null, TestContext.Current.CancellationToken);

        // The owner and its marker-bearing value node are gone...
        Assert.Equal(0, await CountAsync(
            "MATCH (o:ComplexPropertyOwner {Id: $id}) RETURN count(o) AS count",
            new { id = owner.Id }));
        Assert.Equal(0, await CountAsync(
            "MATCH (a:ComplexAddress) WHERE a.City = $city RETURN count(a) AS count",
            new { city = "Complex Vancouver" }));

        // ...but the domain node survives (its edge to the owner is detached with the owner).
        Assert.Equal(1, await CountAsync(
            "MATCH (a:ComplexAddress {Id: $domainId}) RETURN count(a) AS count",
            new { domainId = domainNode.Id }));
    }

    [Fact]
    public async Task Navigation_ComplexPropertyPredicate_MatchesCollidingDomainEdge()
    {
        var (owner, _) = await SeedCollisionAsync(
            complexCity: "Complex Austin", domainCity: "Domain Boulder");

        // The navigation MATCH pattern ((owner)-[:PRIMARY_ADDRESS]->(:ComplexAddress)) does not
        // filter on the marker, so a predicate satisfied ONLY by the domain node still matches.
        var viaDomainValue = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(o => o.Address.City == "Domain Boulder")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(viaDomainValue, o => o.Id == owner.Id);

        // Predicates satisfied by the actual property value match as well, and the materialized
        // value is always the marker-bearing one.
        var viaComplexValue = await Graph.Nodes<ComplexPropertyOwner>()
            .Where(o => o.Address.City == "Complex Austin")
            .ToListAsync(TestContext.Current.CancellationToken);
        var fetched = Assert.Single(viaComplexValue, o => o.Id == owner.Id);
        Assert.Equal("Complex Austin", fetched.Address.City);
    }

    private async Task<(ComplexPropertyOwner Owner, ComplexAddressNode DomainNode)> SeedCollisionAsync(
        string complexCity, string domainCity)
    {
        var owner = new ComplexPropertyOwner
        {
            Address = new ComplexAddress { City = complexCity, Street = "1st Ave" }
        };
        await Graph.CreateNodeAsync(owner, null, TestContext.Current.CancellationToken);

        var domainNode = new ComplexAddressNode { City = domainCity, Street = "2nd Ave" };
        await Graph.CreateNodeAsync(domainNode, null, TestContext.Current.CancellationToken);

        var domainEdge = new PrimaryAddress(owner.Id, domainNode.Id);
        await Graph.CreateRelationshipAsync(domainEdge, null, TestContext.Current.CancellationToken);

        return (owner, domainNode);
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

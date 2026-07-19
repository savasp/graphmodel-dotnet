// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Contract tests for root-node id identity: within one configured graph or store, a node id
/// identifies at most one node regardless of its labels or CLR type. Every provider inherits these.
/// </summary>
/// <remarks>
/// Every id-only surface - relationship endpoints, lookup, update, delete, traversal - resolves
/// through the id alone, so the id must name exactly one node for those surfaces to be well defined.
/// A provider that let two labels hold one id would have to either pick a match or fan out; both are
/// wrong, so the duplicate is rejected at creation instead.
/// </remarks>
public interface INodeIdentityTests : IGraphTest
{
    [Fact]
    public async Task CreateNode_SameIdUnderDifferentLabel_ThrowsAndLeavesExistingNodeUntouched()
    {
        var id = Guid.NewGuid().ToString("N");
        var person = new Person { Id = id, FirstName = "Shared", LastName = "Person" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var address = new Address { Id = id, Street = "1 Graph St", City = "Neo" };
        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(address, null, TestContext.Current.CancellationToken));

        // The rejected create is atomic: the existing node keeps its properties and no node is
        // readable under the second label.
        var storedPerson = await Graph.GetNodeAsync<Person>(id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Shared", storedPerson.FirstName);
        Assert.Equal("Person", storedPerson.LastName);

        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await Graph.GetNodeAsync<Address>(id, null, TestContext.Current.CancellationToken));

        var addresses = await Graph.Nodes<Address>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(addresses);
    }

    [Fact]
    public async Task CreateNode_SameIdUnderDifferentDynamicLabel_Throws()
    {
        var id = Guid.NewGuid().ToString("N");
        var first = new DynamicNode
        {
            Id = id,
            Labels = ["NodeIdentityDynamicFirst"],
            Properties = new Dictionary<string, object?> { ["Name"] = "first" }
        };
        await Graph.CreateNodeAsync(first, null, TestContext.Current.CancellationToken);

        var second = new DynamicNode
        {
            Id = id,
            Labels = ["NodeIdentityDynamicSecond"],
            Properties = new Dictionary<string, object?> { ["Name"] = "second" }
        };

        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(second, null, TestContext.Current.CancellationToken));

        var stored = await Graph.GetDynamicNodeAsync(id, null, TestContext.Current.CancellationToken);
        Assert.Contains("NodeIdentityDynamicFirst", stored.Labels);
        Assert.Equal("first", stored.Properties["Name"]);
    }

    [Fact]
    public async Task CreateNode_DynamicReusingTypedNodeId_Throws()
    {
        // The invariant spans CLR types as well as labels: a dynamic node cannot claim an id that a
        // typed node already holds, nor the reverse.
        var typedId = Guid.NewGuid().ToString("N");
        await Graph.CreateNodeAsync(
            new Person { Id = typedId, FirstName = "Typed" }, null, TestContext.Current.CancellationToken);

        var dynamicNode = new DynamicNode
        {
            Id = typedId,
            Labels = ["NodeIdentityDynamicClaim"],
            Properties = new Dictionary<string, object?> { ["Name"] = "dynamic" }
        };
        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(dynamicNode, null, TestContext.Current.CancellationToken));

        var dynamicId = Guid.NewGuid().ToString("N");
        await Graph.CreateNodeAsync(
            new DynamicNode
            {
                Id = dynamicId,
                Labels = ["NodeIdentityDynamicHolder"],
                Properties = new Dictionary<string, object?> { ["Name"] = "holder" }
            },
            null,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(
                new Person { Id = dynamicId, FirstName = "Claimant" },
                null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateSubgraph_EndpointReusesIdUnderDifferentLabel_CreatesNothing()
    {
        var sharedId = Guid.NewGuid().ToString("N");
        await Graph.CreateNodeAsync(
            new Address { Id = sharedId, Street = "1 Graph St", City = "Neo" },
            null,
            TestContext.Current.CancellationToken);

        // The subgraph's source reuses the address id under a different label, so the whole
        // statement must fail without creating the target or the edge.
        var source = new Person { Id = sharedId, FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken));

        var people = await Graph.Nodes<Person>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(people);

        var relationships = await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(relationships);

        var storedAddress = await Graph.GetNodeAsync<Address>(sharedId, null, TestContext.Current.CancellationToken);
        Assert.Equal("1 Graph St", storedAddress.Street);
    }

    [Fact]
    public async Task CreateRelationship_UniqueEndpoints_CreatesExactlyOneRelationship()
    {
        // The endpoint ids each resolve to exactly one node, so one API call stores one edge - never
        // a cross-product over several nodes sharing an endpoint id.
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        await Graph.CreateNodeAsync(source, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, null, TestContext.Current.CancellationToken);

        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };
        await Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var relationships = await Graph.Relationships<Knows>().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(relationships);
        Assert.Equal(source.Id, relationships[0].StartNodeId);
        Assert.Equal(target.Id, relationships[0].EndNodeId);

        // One stored edge also means one traversal path.
        var reachable = await Graph.Nodes<Person>()
            .Where(p => p.Id == source.Id)
            .PathSegments<Person, Knows, Person>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(reachable);
    }

    [Fact]
    public async Task RelationshipMayReuseNodeId()
    {
        // Node ids and relationship ids are separate namespaces: enforcing node id uniqueness must
        // not couple them.
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        await Graph.CreateNodeAsync(source, null, TestContext.Current.CancellationToken);
        await Graph.CreateNodeAsync(target, null, TestContext.Current.CancellationToken);

        var knows = new Knows { Id = source.Id, StartNodeId = source.Id, EndNodeId = target.Id };
        await Graph.CreateRelationshipAsync(knows, null, TestContext.Current.CancellationToken);

        var storedRelationship = await Graph.GetRelationshipAsync<Knows>(
            source.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal(source.Id, storedRelationship.StartNodeId);

        var storedNode = await Graph.GetNodeAsync<Person>(source.Id, null, TestContext.Current.CancellationToken);
        Assert.Equal("Alice", storedNode.FirstName);
    }

    [Fact]
    [RequiresCapability(GraphCapability.Transactions)]
    public async Task ConcurrentCreates_SameIdUnderDifferentLabels_LetExactlyOneCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid().ToString("N");
        var barrier = new AsyncBarrier(2);

        // Both writers open a transaction, meet at the barrier, and only then create - so the two
        // creates genuinely race rather than being ordered by setup cost.
        var failures = await Task.WhenAll(
            CreateInOwnTransactionAsync(
                new Person { Id = id, FirstName = "Racing" }, barrier, cancellationToken),
            CreateInOwnTransactionAsync(
                new Address { Id = id, Street = "1 Race St" }, barrier, cancellationToken));

        Assert.Single(failures.Where(failure => failure is null));

        // Exactly one node carries the id, and it is readable under exactly one of the two labels.
        var people = await Graph.Nodes<Person>().Where(p => p.Id == id)
            .ToListAsync(cancellationToken);
        var addresses = await Graph.Nodes<Address>().Where(a => a.Id == id)
            .ToListAsync(cancellationToken);
        Assert.Equal(1, people.Count + addresses.Count);
    }

    private async Task<Exception?> CreateInOwnTransactionAsync<TNode>(
        TNode node,
        AsyncBarrier barrier,
        CancellationToken cancellationToken)
        where TNode : class, INode
    {
        var barrierSignaled = false;
        try
        {
            await using var transaction = await Graph.GetTransactionAsync(cancellationToken);
            barrierSignaled = true;
            await barrier.SignalAndWaitAsync(cancellationToken);
            await Graph.CreateNodeAsync(node, transaction, cancellationToken);
            await transaction.CommitAsync();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!barrierSignaled)
            {
                barrier.Signal();
            }

            return exception;
        }
    }
}

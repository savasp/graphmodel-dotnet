// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph.Neo4j.Core;
using global::Neo4j.Driver;


/// <summary>
/// Pins the storage mechanism behind the graph-wide node id invariant (#369). The compatibility
/// suite can only observe the behaviour; these tests observe how it is achieved, because the whole
/// invariant rests on the reserved label being written to every root node, withheld from every
/// complex-property value node, and hidden from callers.
/// </summary>
public class RootNodeLabelTests(Neo4jHarness harness) :
    Neo4jTest(harness)
{
    private const string RootNodeLabel = "__CvoyaRootNode";

    [Fact]
    public async Task CreatedRootNode_CarriesReservedLabel_ButDoesNotExposeIt()
    {
        var person = new Person { FirstName = "Alice" };
        await Graph.CreateNodeAsync(person, null, TestContext.Current.CancellationToken);

        var storedLabels = await ReadStoredLabelsAsync(person.Id);
        Assert.Contains(RootNodeLabel, storedLabels);
        Assert.Contains(nameof(Person), storedLabels);

        // The reserved label is infrastructure: a typed read is unaffected, and a dynamic read - the
        // one surface that returns labels verbatim - must not leak it.
        var dynamicNode = await Graph.GetDynamicNodeAsync(person.Id, null, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(RootNodeLabel, dynamicNode.Labels);
        Assert.Contains(nameof(Person), dynamicNode.Labels);
    }

    [Fact]
    public async Task ComplexPropertyValueNode_DoesNotCarryReservedLabel()
    {
        // Value nodes get provider-generated ids, so including them in the constraint would make the
        // invariant cover ids callers never chose.
        var suffix = Guid.NewGuid().ToString("N");
        var node = new Class1
        {
            Property1 = $"root-{suffix}",
            A = new ComplexClassA { Property1 = $"value-{suffix}" }
        };

        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var labelledValueNodes = await ReadCountAsync(
            $@"MATCH (valueNode:{nameof(ComplexClassA)} {{ Property1: $propertyValue }})
               WHERE valueNode:{RootNodeLabel}
               RETURN COUNT(valueNode) AS count",
            new { propertyValue = $"value-{suffix}" });
        Assert.Equal(0, labelledValueNodes);
    }

    [Fact]
    public async Task DynamicNodeLabelChange_KeepsReservedLabel()
    {
        // Updating a dynamic node's labels removes the previous ones. The reserved label must survive
        // that swap, or the node silently drops out of the uniqueness constraint.
        var node = new DynamicNode
        {
            Labels = ["RootLabelBefore"],
            Properties = new Dictionary<string, object?> { ["Name"] = "before" }
        };
        await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken);

        var updated = new DynamicNode
        {
            Id = node.Id,
            Labels = ["RootLabelAfter"],
            Properties = new Dictionary<string, object?> { ["Name"] = "after" }
        };
        await Graph.UpdateNodeAsync(updated, null, TestContext.Current.CancellationToken);

        var storedLabels = await ReadStoredLabelsAsync(node.Id);
        Assert.Contains(RootNodeLabel, storedLabels);
        Assert.Contains("RootLabelAfter", storedLabels);
        Assert.DoesNotContain("RootLabelBefore", storedLabels);
    }

    [Fact]
    public async Task DynamicNode_CannotClaimReservedLabel()
    {
        var node = new DynamicNode(
            [RootNodeLabel],
            new Dictionary<string, object?> { ["Name"] = "caller-owned" });

        var failure = await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(node, null, TestContext.Current.CancellationToken));

        Assert.Contains(RootNodeLabel, failure.Message, StringComparison.Ordinal);
        Assert.Contains("reserved for provider infrastructure", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicLabelQueries_DoNotMatchReservedLabel()
    {
        await Graph.CreateNodeAsync(
            new DynamicNode(["PublicRootLabelTest"], new Dictionary<string, object?>()),
            null,
            TestContext.Current.CancellationToken);

        var filtered = await Graph.DynamicNodes()
            .OfLabel(RootNodeLabel)
            .ToListAsync(TestContext.Current.CancellationToken);
        var dynamicPredicate = await Graph.DynamicNodes()
            .Where(node => node.HasLabel(RootNodeLabel))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(filtered);
        Assert.Empty(dynamicPredicate);
    }

    [Fact]
    public async Task DuplicateId_ReportsTheContractNotTheInternalLabel()
    {
        // The database words a constraint breach in terms of the reserved label, which is an
        // implementation detail. The provider must translate it, and this asserts the translation
        // actually fires - a detection predicate that never matches would leave the raw driver
        // wording in place while every behavioural test still passed.
        var id = Guid.NewGuid().ToString("N");
        await Graph.CreateNodeAsync(
            new Person { Id = id, FirstName = "First" }, null, TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<GraphException>(async () =>
            await Graph.CreateNodeAsync(
                new Address { Id = id, Street = "1 Graph St" }, null, TestContext.Current.CancellationToken));

        Assert.Contains("unique across all labels", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(RootNodeLabel, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubgraphEndpoints_CarryReservedLabel()
    {
        var source = new Person { FirstName = "Alice" };
        var target = new Person { FirstName = "Bob" };
        var knows = new Knows { StartNodeId = source.Id, EndNodeId = target.Id };

        await Graph.CreateAsync(source, knows, target, null, null, TestContext.Current.CancellationToken);

        Assert.Contains(RootNodeLabel, await ReadStoredLabelsAsync(source.Id));
        Assert.Contains(RootNodeLabel, await ReadStoredLabelsAsync(target.Id));
    }

    private async Task<IReadOnlyList<string>> ReadStoredLabelsAsync(string nodeId)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        var result = await neo4jTransaction.Transaction.RunAsync(
            "MATCH (n {Id: $nodeId}) RETURN labels(n) AS labels",
            new { nodeId });
        var records = await result.ToListAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        return Assert.Single(records)["labels"].As<List<string>>();
    }

    private async Task<int> ReadCountAsync(string cypher, object parameters)
    {
        await using var transaction = await Graph.GetTransactionAsync(TestContext.Current.CancellationToken);
        var neo4jTransaction = (GraphTransaction)transaction;

        var result = await neo4jTransaction.Transaction.RunAsync(cypher, parameters);
        var record = await result.SingleAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync();

        return record["count"].As<int>();
    }
}

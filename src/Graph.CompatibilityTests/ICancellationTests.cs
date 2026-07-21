// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public interface ICancellationTests : IGraphTest
{
    [Fact]
    public async Task PreCancelledToken_ThrowsOperationCanceledException_ForCrudAsyncEntryPoints()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var node = new Person { FirstName = "Cancelled", LastName = "Crud" };
        var relationship = new Knows();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetTransactionAsync(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.CreateNodeAsync(node, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.FindNodeByTestKeyAsync<Person>(node.TestKey, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SelectNode(node).UpdateAsync(
                setters => setters.SetProperty(candidate => candidate.LastName, "Updated"),
                cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SelectNode(node).DeleteAsync(cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.FindDynamicNodeByTestKeyAsync(node.TestKey, null, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.CreateRelationshipAsync(
                Graph.SelectNode(node),
                relationship,
                Graph.Nodes<Person>().Where(candidate => candidate.FirstName == "Target"),
                cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.FindRelationshipByTestKeyAsync<Knows>(relationship.TestKey, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SelectRelationship(relationship).UpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Since, DateTime.UtcNow),
                cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SelectRelationship(relationship).DeleteAsync(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.FindDynamicRelationshipByTestKeyAsync(relationship.TestKey, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.RecreateManagedIndexesAsync(cts.Token));
    }

    [Fact]
    public async Task PreCancelledToken_ThrowsOperationCanceledException_ForBufferedQueryPath()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.Nodes<Person>().ToListAsync(cts.Token));
    }

    [Fact]
    public async Task PreCancelledToken_ThrowsOperationCanceledException_ForStreamingQueryPath()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var enumerator = Graph.Nodes<Person>().GetAsyncEnumerator(cts.Token);
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    [Fact]
    public async Task PreCancelledToken_ThrowsOperationCanceledException_ForSearchPath()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.Search("cancelled-search").ToListAsync(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SearchNodes<Person>("cancelled-search").ToListAsync(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.SearchRelationships<Knows>("cancelled-search").ToListAsync(cts.Token));
    }
}

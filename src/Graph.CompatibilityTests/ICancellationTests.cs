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
        var relationship = new Knows
        {
            StartNodeId = Guid.NewGuid().ToString("N"),
            EndNodeId = Guid.NewGuid().ToString("N")
        };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetTransactionAsync(cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.CreateNodeAsync(node, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetNodeAsync<Person>(node.Id, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.UpdateNodeAsync(node, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.DeleteNodeAsync(node.Id, false, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetDynamicNodeAsync(node.Id, null, cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.CreateRelationshipAsync(relationship, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetRelationshipAsync<Knows>(relationship.Id, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.UpdateRelationshipAsync(relationship, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.DeleteRelationshipAsync(relationship.Id, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.GetDynamicRelationshipAsync(relationship.Id, null, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Graph.RecreateIndexesAsync(cts.Token));
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

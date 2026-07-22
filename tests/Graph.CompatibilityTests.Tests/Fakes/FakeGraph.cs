// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests.Tests.Fakes;

/// <summary>
/// A minimal <see cref="IGraph"/> that exists only so a <see cref="IGraphProviderTestHarness"/>
/// fake has something to hand back from <see cref="IGraphProviderTestHarness.GetGraphAsync"/>.
/// Nothing in the meta-test suite actually calls these members; every method beyond
/// <see cref="SchemaRegistry"/> throws.
/// </summary>
internal sealed class FakeGraph : IGraph, IAsyncDisposable
{
    private readonly Action? onDispose;

    public FakeGraph(Action? onDispose = null)
    {
        this.onDispose = onDispose;
    }

    public SchemaRegistry SchemaRegistry { get; } = new();

    public ValueTask DisposeAsync()
    {
        onDispose?.Invoke();
        return ValueTask.CompletedTask;
    }

    public Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IGraphQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) =>
        throw new NotSupportedException();

    public IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) =>
        throw new NotSupportedException();

    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : class, INode =>
        throw new NotSupportedException();

    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : class, IRelationship =>
        throw new NotSupportedException();

    public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : class, INode =>
        throw new NotSupportedException();

    public Task CreateAsync<TSource, TRelationship, TTarget>(TSource source, TRelationship relationship, TTarget target, RelationshipDirection direction = RelationshipDirection.Outgoing, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode =>
        throw new NotSupportedException();

    public IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null) =>
        throw new NotSupportedException();

    public IGraphQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null) =>
        throw new NotSupportedException();

    public IGraphQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null) =>
        throw new NotSupportedException();

    public IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : class, INode =>
        throw new NotSupportedException();

    public IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : class, IRelationship =>
        throw new NotSupportedException();

    public Task RecreateManagedIndexesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

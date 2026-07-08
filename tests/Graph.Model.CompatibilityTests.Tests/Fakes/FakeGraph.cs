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

namespace Cvoya.Graph.Model.CompatibilityTests.Tests.Fakes;

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

    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : class, INode =>
        throw new NotSupportedException();

    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : class, IRelationship =>
        throw new NotSupportedException();

    public Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : class, INode =>
        throw new NotSupportedException();

    public Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : class, IRelationship =>
        throw new NotSupportedException();

    public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : class, INode =>
        throw new NotSupportedException();

    public Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : class, IRelationship =>
        throw new NotSupportedException();

    public Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : class, INode =>
        throw new NotSupportedException();

    public Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : class, IRelationship =>
        throw new NotSupportedException();

    public Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
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

    public Task RecreateIndexesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

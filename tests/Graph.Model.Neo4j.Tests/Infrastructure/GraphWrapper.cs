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

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Configuration;

public class GraphWrapper : IGraph
{
    private readonly IGraph innerGraph;
    private readonly Func<ValueTask> onDispose;
    private bool disposed;
    private string databaseName = string.Empty;

    public GraphWrapper(IGraph innerGraph, string databaseName, Func<ValueTask> onDispose)
    {
        this.databaseName = databaseName;
        this.innerGraph = innerGraph;
        this.onDispose = onDispose;
    }

    internal string DatabaseName => databaseName;

    public PropertyConfigurationRegistry PropertyConfigurationRegistry => innerGraph.PropertyConfigurationRegistry;

    public Task<IGraphTransaction> GetTransactionAsync() => innerGraph.GetTransactionAsync();

    public IGraphNodeQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) =>
        innerGraph.DynamicNodes(transaction);

    public IGraphRelationshipQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) =>
        innerGraph.DynamicRelationships(transaction);

    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        innerGraph.GetDynamicNodeAsync(id, transaction, cancellationToken);

    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        innerGraph.GetDynamicRelationshipAsync(id, transaction, cancellationToken);

    public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : INode =>
        innerGraph.Nodes<N>(transaction);

    public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : IRelationship =>
        innerGraph.Relationships<R>(transaction);

    public Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode =>
        innerGraph.GetNodeAsync<N>(id, transaction, cancellationToken);

    public Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship =>
        innerGraph.GetRelationshipAsync<R>(id, transaction, cancellationToken);

    public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode =>
        innerGraph.CreateNodeAsync(node, transaction, cancellationToken);

    public Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship =>
        innerGraph.CreateRelationshipAsync(relationship, transaction, cancellationToken);

    public Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode =>
        innerGraph.UpdateNodeAsync(node, transaction, cancellationToken);

    public Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship =>
        innerGraph.UpdateRelationshipAsync(relationship, transaction, cancellationToken);

    public Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        innerGraph.DeleteNodeAsync(id, cascadeDelete, transaction, cancellationToken);

    public Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) =>
        innerGraph.DeleteRelationshipAsync(id, transaction, cancellationToken);

    public IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null) =>
        innerGraph.Search(query, transaction);

    public IGraphNodeQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null) =>
        innerGraph.SearchNodes(query, transaction);

    public IGraphRelationshipQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null) =>
        innerGraph.SearchRelationships(query, transaction);

    public IGraphNodeQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : INode =>
        innerGraph.SearchNodes<T>(query, transaction);

    public IGraphRelationshipQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : IRelationship =>
        innerGraph.SearchRelationships<T>(query, transaction);

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;

        disposed = true;

        // First dispose the inner graph
        await innerGraph.DisposeAsync();

        // Then call the custom disposal action
        await onDispose();
    }
}
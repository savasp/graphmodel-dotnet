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

namespace Cvoya.Graph.Model.Age.Core;

using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Apache AGE implementation of <see cref="IGraph"/>. The implementation will be filled in as the provider matures.
/// </summary>
internal sealed class AgeGraph : IGraph
{
    private readonly ILogger logger;

    public AgeGraph(AgeGraphStore store, string graphName, SchemaRegistry schemaRegistry, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Store = store;
        GraphName = graphName;
        SchemaRegistry = schemaRegistry;
        logger = loggerFactory.CreateLogger<AgeGraph>();
        logger.LogInformation("Initialized Apache AGE graph '{GraphName}'", GraphName);
    }

    internal AgeGraphStore Store { get; }

    internal string GraphName { get; }

    /// <inheritdoc />
    public SchemaRegistry SchemaRegistry { get; }

    /// <inheritdoc />
    public Task<IGraphTransaction> GetTransactionAsync() => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphNodeQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphRelationshipQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : INode => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : IRelationship => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotImplementedException();

    /// <inheritdoc />
    public Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotImplementedException();

    /// <inheritdoc />
    public Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotImplementedException();

    /// <inheritdoc />
    public Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode => throw new NotImplementedException();

    /// <inheritdoc />
    public Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship => throw new NotImplementedException();

    /// <inheritdoc />
    public Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphNodeQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphRelationshipQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphNodeQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : INode => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphRelationshipQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : IRelationship => throw new NotImplementedException();

    /// <inheritdoc />
    public Task RecreateIndexesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

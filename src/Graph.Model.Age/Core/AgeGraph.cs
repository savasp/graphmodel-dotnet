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

using System.Collections.Concurrent;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core.Entities;
using Cvoya.Graph.Model.Age.Core.Internal;
using Microsoft.Extensions.Logging;
using Npgsql;

/// <summary>
/// Apache AGE implementation of <see cref="IGraph"/>. Works with a single active connection.
/// </summary>
internal sealed class AgeGraph : IGraph
{
    private readonly ILogger logger;
    private readonly AgeGraphContext graphContext;
    private readonly AgeNodeManager nodeManager;
    private readonly AgeRelationshipManager relationshipManager;
    private readonly ConcurrentBag<AgeGraphTransaction> activeTransactions = new();
    private NpgsqlConnection connection;
    private bool isDisposed;

    public AgeGraph(NpgsqlConnection connection, string graphName, SchemaRegistry schemaRegistry, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this.connection = connection;
        GraphName = graphName;
        SchemaRegistry = schemaRegistry;
        logger = loggerFactory.CreateLogger<AgeGraph>();
        logger.LogInformation("Initialized Apache AGE graph '{GraphName}'", GraphName);

        graphContext = new AgeGraphContext(this, connection, GraphName, SchemaRegistry, loggerFactory);
        nodeManager = graphContext.NodeManager;
        relationshipManager = graphContext.RelationshipManager;
    }

    internal string GraphName { get; }

    /// <inheritdoc />
    public SchemaRegistry SchemaRegistry { get; }

    /// <inheritdoc />
    public async Task<IGraphTransaction> GetTransactionAsync()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(AgeGraph));
        }

        return await GraphOperationHelper.ExecuteAsync(
            logger,
            "Failed to create AGE transaction",
            async () =>
            {
                var transaction = graphContext.CreateTransaction();
                await transaction.BeginTransactionAsync().ConfigureAwait(false);
                activeTransactions.Add(transaction);
                return transaction;
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IGraphNodeQueryable<DynamicNode>> DynamicNodesAsync(IGraphTransaction? transaction = null)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IGraphRelationshipQueryable<DynamicRelationship>> DynamicRelationshipsAsync(IGraphTransaction? transaction = null)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IGraphNodeQueryable<N>> NodesAsync<N>(IGraphTransaction? transaction = null)
        where N : INode
    {
        return GraphOperationHelper.ExecuteSync(
            logger,
            $"Failed to create nodes queryable for type {typeof(N).Name}",
            () =>
            {
                logger.LogDebug("Getting nodes queryable for type {NodeType}", typeof(N).Name);
                var provider = new Querying.Linq.Providers.AgeGraphQueryProvider(graphContext, (AgeGraphTransaction?)transaction);
                var queryable = new Querying.Linq.Queryables.AgeGraphNodeQueryable<N>(provider, graphContext);
                return Task.FromResult<IGraphNodeQueryable<N>>(queryable);
            });
    }

    /// <inheritdoc />
    public Task<IGraphRelationshipQueryable<R>> RelationshipsAsync<R>(IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        return GraphOperationHelper.ExecuteSync(
            logger,
            $"Failed to create relationships queryable for type {typeof(R).Name}",
            () =>
            {
                logger.LogDebug("Getting relationships queryable for type {RelationshipType}", typeof(R).Name);
                var ageTx = (AgeGraphTransaction?)transaction;
                var provider = new Querying.Linq.Providers.AgeGraphQueryProvider(graphContext, ageTx);
                var queryable = new Querying.Linq.Queryables.AgeGraphRelationshipQueryable<R>(provider, graphContext);
                return Task.FromResult<IGraphRelationshipQueryable<R>>(queryable);
            });
    }

    /// <inheritdoc />
    public async Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        return await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to retrieve node {id} of type {typeof(N).Name}",
            async () => await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => nodeManager.GetNodeAsync<N>(id, tx, cancellationToken),
                    $"Failed to retrieve node {id} of type {typeof(N).Name}",
                    logger,
                    isReadOnly: true)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to retrieve relationship {id} of type {typeof(R).Name}",
            async () => await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => relationshipManager.GetRelationshipAsync<R>(id, tx, cancellationToken),
                    $"Failed to retrieve relationship {id} of type {typeof(R).Name}",
                    logger,
                    isReadOnly: true)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        if (node is null) throw new ArgumentException("Node cannot be null.", nameof(node));
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to create node of type {typeof(N).Name}",
            async () =>
            {
                await TransactionHelpers.ExecuteInTransactionAsync(
                        graphContext,
                        transaction,
                        async tx =>
                        {
                            await nodeManager.CreateNodeAsync(node, tx, cancellationToken).ConfigureAwait(false);
                            return true;
                        },
                        $"Failed to create node of type {typeof(N).Name}",
                        logger,
                        isReadOnly: false)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        if (relationship is null) throw new ArgumentException("Relationship cannot be null.", nameof(relationship));
        if (string.IsNullOrWhiteSpace(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to create relationship of type {typeof(R).Name}",
            async () =>
            {
                await TransactionHelpers.ExecuteInTransactionAsync(
                        graphContext,
                        transaction,
                        async tx =>
                        {
                            await relationshipManager.CreateRelationshipAsync(relationship, tx, cancellationToken).ConfigureAwait(false);
                            return true;
                        },
                        $"Failed to create relationship of type {typeof(R).Name}",
                        logger,
                        isReadOnly: false)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to update node {node.Id} of type {typeof(N).Name}",
            async () => await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => nodeManager.UpdateNodeAsync(node, tx, cancellationToken),
                    $"Failed to update node {node.Id} of type {typeof(N).Name}",
                    logger,
                    isReadOnly: false)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);
        if (string.IsNullOrWhiteSpace(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}",
            async () => await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => relationshipManager.UpdateRelationshipAsync(relationship, tx, cancellationToken),
                    $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}",
                    logger,
                    isReadOnly: false)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to delete node {id}",
            async () =>
            {
                var deleted = await TransactionHelpers.ExecuteInTransactionAsync(
                        graphContext,
                        transaction,
                        tx => nodeManager.DeleteNodeAsync(id, tx, cascadeDelete, cancellationToken),
                        $"Failed to delete node {id}",
                        logger,
                        isReadOnly: false)
                    .ConfigureAwait(false);

                if (!deleted)
                {
                    throw new GraphException($"Node {id} was not deleted.");
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await GraphOperationHelper.ExecuteAsync(
            logger,
            $"Failed to delete relationship {id}",
            async () =>
            {
                var deleted = await TransactionHelpers.ExecuteInTransactionAsync(
                        graphContext,
                        transaction,
                        tx => relationshipManager.DeleteRelationshipAsync(id, tx, cancellationToken),
                        $"Failed to delete relationship {id}",
                        logger,
                        isReadOnly: false)
                    .ConfigureAwait(false);

                if (!deleted)
                {
                    throw new GraphException($"Relationship {id} was not deleted.");
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IGraphQueryable<IEntity>> SearchAsync(string query, IGraphTransaction? transaction = null)
        => throw new NotSupportedException("Full-text search is not supported in Apache AGE. See docs/age-fulltext-search-limitations.md for details.");

    /// <inheritdoc />
    public Task<IGraphNodeQueryable<INode>> SearchNodesAsync(string query, IGraphTransaction? transaction = null)
        => throw new NotSupportedException("Full-text search is not supported in Apache AGE. See docs/age-fulltext-search-limitations.md for details.");

    /// <inheritdoc />
    public Task<IGraphRelationshipQueryable<IRelationship>> SearchRelationshipsAsync(string query, IGraphTransaction? transaction = null)
        => throw new NotSupportedException("Full-text search is not supported in Apache AGE. See docs/age-fulltext-search-limitations.md for details.");

    /// <inheritdoc />
    public Task<IGraphNodeQueryable<T>> SearchNodesAsync<T>(string query, IGraphTransaction? transaction = null)
        where T : INode
        => throw new NotSupportedException("Full-text search is not supported in Apache AGE. See docs/age-fulltext-search-limitations.md for details.");

    /// <inheritdoc />
    public Task<IGraphRelationshipQueryable<T>> SearchRelationshipsAsync<T>(string query, IGraphTransaction? transaction = null)
        where T : IRelationship
        => throw new NotSupportedException("Full-text search is not supported in Apache AGE. See docs/age-fulltext-search-limitations.md for details.");

    /// <inheritdoc />
    public Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        logger.LogDebug("Disposing AGE graph '{GraphName}' and cleaning up {Count} active transactions", GraphName, activeTransactions.Count);

        foreach (var transaction in activeTransactions)
        {
            try
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error disposing transaction during graph cleanup");
            }
        }

        activeTransactions.Clear();

        try
        {
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                logger.LogDebug("Connection closed for AGE graph '{GraphName}'", GraphName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error closing connection during graph cleanup");
        }

        logger.LogDebug("AGE graph '{GraphName}' disposed successfully", GraphName);
    }
}

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
/// The implementation will be filled in as the provider matures.
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

        try
        {
            var transaction = graphContext.CreateTransaction();
            await transaction.BeginTransactionAsync().ConfigureAwait(false);
            activeTransactions.Add(transaction);
            return transaction;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create AGE transaction");
            throw new GraphException("Failed to create AGE transaction", ex);
        }
    }

    /// <inheritdoc />
    public IGraphNodeQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphRelationshipQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : INode
    {
        try
        {
            logger.LogDebug("Getting nodes queryable for type {NodeType}", typeof(N).Name);

            // Create a provider scoped to this specific transaction
            var provider = new Querying.Linq.Providers.AgeGraphQueryProvider(graphContext,(AgeGraphTransaction?) transaction);
            return new Querying.Linq.Queryables.AgeGraphNodeQueryable<N>(provider, graphContext);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create nodes queryable for type {typeof(N).Name}";
            logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : IRelationship
    {
        try
        {
            logger.LogDebug("Getting relationships queryable for type {RelationshipType}", typeof(R).Name);

            // Only request read-only if we're creating a new transaction
            // If a transaction is provided, use it as-is (could be read or write)
            var ageTx = TransactionHelpers.GetOrCreateTransactionAsync(graphContext, transaction, transaction == null).GetAwaiter().GetResult();

            // Create a provider scoped to this specific transaction
            var provider = new Querying.Linq.Providers.AgeGraphQueryProvider(graphContext, ageTx);
            return new Querying.Linq.Queryables.AgeGraphRelationshipQueryable<R>(provider, graphContext);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create relationships queryable for type {typeof(R).Name}";
            logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        // Use the query infrastructure like Neo4j does - this will properly handle complex properties
        var query = Nodes<N>(transaction)
            .Where(n => n.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new GraphException($"Node with ID {id} not found");
    }

    /// <inheritdoc />
    public async Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            return await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => relationshipManager.GetRelationshipAsync<R>(id, tx, cancellationToken),
                    $"Failed to retrieve relationship {id} of type {typeof(R).Name}",
                    isReadOnly: true)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = $"Failed to retrieve relationship {id} of type {typeof(R).Name}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        if (node is null)
            throw new ArgumentException("Node cannot be null.", nameof(node));
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        try
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
                    isReadOnly: false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        if (relationship is null)
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));
        if (string.IsNullOrWhiteSpace(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        try
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
                    isReadOnly: false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = $"Failed to create relationship of type {typeof(R).Name}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : INode
    {
        if (node is null)
            throw new ArgumentException("Node cannot be null.", nameof(node));
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        try
        {
            await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => nodeManager.UpdateNodeAsync(node, tx, cancellationToken),
                    $"Failed to update node {node.Id} of type {typeof(N).Name}",
                    isReadOnly: false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = $"Failed to update node {node.Id} of type {typeof(N).Name}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : IRelationship
    {
        if (relationship is null)
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));
        if (string.IsNullOrWhiteSpace(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        try
        {
            await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => relationshipManager.UpdateRelationshipAsync(relationship, tx, cancellationToken),
                    $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}",
                    isReadOnly: false)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var deleted = await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => nodeManager.DeleteNodeAsync(id, tx, cascadeDelete, cancellationToken),
                    $"Failed to delete node {id}",
                    isReadOnly: false)
                .ConfigureAwait(false);

            if (!deleted)
            {
                throw new GraphException($"Node {id} was not deleted.");
            }
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete node {id}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        try
        {
            var deleted = await TransactionHelpers.ExecuteInTransactionAsync(
                    graphContext,
                    transaction,
                    tx => relationshipManager.DeleteRelationshipAsync(id, tx, cancellationToken),
                    $"Failed to delete relationship {id}",
                    isReadOnly: false)
                .ConfigureAwait(false);

            if (!deleted)
            {
                throw new GraphException($"Relationship {id} was not deleted.");
            }
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete relationship {id}";
            logger.LogError(ex, message);
            if (ex is GraphException)
            {
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

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
    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        logger.LogDebug("Disposing AGE graph '{GraphName}' and cleaning up {Count} active transactions", GraphName, activeTransactions.Count);

        // Dispose all active transactions
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

        // Close the connection since AgeGraph has a 1:1 relationship with AgeGraphContext
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

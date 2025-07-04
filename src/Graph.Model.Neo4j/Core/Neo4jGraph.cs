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

namespace Cvoya.Graph.Model.Neo4j.Core;

using Cvoya.Graph.Model.Configuration;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;

using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Neo4j implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
internal class Neo4jGraph : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly GraphContext _graphContext;
    private readonly PropertyConfigurationRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraph"/> class.
    /// </summary>
    public Neo4jGraph(IDriver driver, string databaseName, PropertyConfigurationRegistry? registry = null, ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<Neo4jGraph>() ?? NullLogger<Neo4jGraph>.Instance;
        _registry = registry ?? new PropertyConfigurationRegistry();

        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _graphContext = new GraphContext(
            this,
            driver,
            databaseName,
            loggerFactory,
            _registry);

        _logger.LogInformation("Graph initialized for database '{0}'", databaseName);
    }

    /// <inheritdoc />
    public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : Model.INode
    {
        try
        {
            _logger.LogDebug("Getting nodes queryable for type {NodeType}", typeof(N).Name);

            var neo4jTx = TransactionHelpers.GetOrCreateTransactionAsync(_graphContext, transaction, true).Result;

            // Create a provider scoped to this specific transaction
            var provider = new GraphQueryProvider(_graphContext, neo4jTx);
            return new GraphNodeQueryable<N>(provider, neo4jTx, _graphContext);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create nodes queryable for type {typeof(N).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : Model.IRelationship
    {
        try
        {
            _logger.LogDebug("Getting relationships queryable for type {RelationshipType}", typeof(R).Name);

            var neo4jTx = TransactionHelpers.GetOrCreateTransactionAsync(_graphContext, transaction, true).Result;

            // Create a provider scoped to this specific transaction
            var provider = new GraphQueryProvider(_graphContext, neo4jTx);
            return new GraphRelationshipQueryable<R>(provider, _graphContext, neo4jTx);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create relationships queryable for type {typeof(R).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : Model.INode
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));

        try
        {
            _logger.LogDebug("Getting node {NodeId} of type {NodeType}", id, typeof(N).Name);

            var result = await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.NodeManager.GetNodeAsync<N>(id, tx, cancellationToken),
                $"Failed to get node {id} of type {typeof(N).Name}",
                _logger);

            if (result is null)
            {
                throw new KeyNotFoundException($"Node with ID '{id}' not found.");
            }

            _logger.LogDebug("Successfully retrieved node {NodeId}", id);
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get node {id} of type {typeof(N).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : Model.IRelationship
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));

        try
        {
            _logger.LogDebug("Getting relationship {RelationshipId} of type {RelationshipType}", id, typeof(R).Name);

            var result = await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.RelationshipManager.GetRelationshipAsync<R>(id, tx, cancellationToken),
                $"Failed to get relationship {id} of type {typeof(R).Name}",
                _logger);

            if (result is null)
            {
                throw new KeyNotFoundException($"Relationship with ID '{id}' not found.");
            }

            _logger.LogDebug("Successfully retrieved relationship {RelationshipId}", id);
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get relationship {id} of type {typeof(R).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : Model.INode
    {
        if (node is null)
            throw new ArgumentException(nameof(node), "Node cannot be null.");

        if (string.IsNullOrEmpty(node.Id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node.Id));

        try
        {
            _logger.LogDebug("Creating node of type {NodeType}", typeof(N).Name);

            await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.NodeManager.CreateNodeAsync(node, tx, cancellationToken),
                $"Failed to create node of type {typeof(N).Name}");

            _logger.LogDebug("Successfully created node {NodeId}", node.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : Model.IRelationship
    {
        if (relationship is null)
            throw new ArgumentException(nameof(relationship), "Relationship cannot be null.");

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship.Id));

        try
        {
            _logger.LogDebug("Creating relationship of type {RelationshipType}", typeof(R).Name);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.CreateRelationshipAsync(relationship, tx);
                    return true;
                },
                $"Failed to create relationship of type {typeof(R).Name}");

            _logger.LogDebug("Successfully created relationship {RelationshipId}", relationship.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create relationship of type {typeof(R).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : Model.INode
    {
        if (node is null)
            throw new ArgumentException(nameof(node), "Node cannot be null.");

        if (string.IsNullOrEmpty(node.Id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node.Id));

        GraphDataModel.EnforceGraphConstraintsForNode(node);

        try
        {
            _logger.LogDebug("Updating node {NodeId} of type {NodeType}", node.Id, typeof(N).Name);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.UpdateNodeAsync(node, tx);
                    return true;
                },
            $"Failed to update node {node.Id} of type {typeof(N).Name}");

            _logger.LogDebug("Successfully updated node {NodeId}", node.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to update node {node.Id} of type {typeof(N).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : Model.IRelationship
    {
        if (relationship is null)
            throw new ArgumentException(nameof(relationship), "Relationship cannot be null.");

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship.Id));

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        try
        {
            _logger.LogDebug("Updating relationship {RelationshipId} of type {RelationshipType}", relationship.Id, typeof(R).Name);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.UpdateRelationshipAsync(relationship, tx);
                    return true;
                },
            $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}");

            _logger.LogDebug("Successfully updated relationship {RelationshipId}", relationship.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<IGraphTransaction> GetTransactionAsync()
    {
        try
        {
            _logger.LogDebug("Beginning new transaction");

            var session = _graphContext.Driver.AsyncSession(o => o.WithDatabase(_graphContext.DatabaseName));
            var transaction = await session.BeginTransactionAsync();
            var graphTransaction = new GraphTransaction(session, transaction);

            _logger.LogDebug("Successfully began transaction");
            return graphTransaction;
        }
        catch (Exception ex)
        {
            const string message = "Failed to begin transaction";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));

        try
        {
            _logger.LogDebug("Deleting node {NodeId}", id);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.DeleteNodeAsync(id, tx, cascadeDelete, cancellationToken);
                    return true;
                },
                $"Failed to delete node {id}");

            _logger.LogDebug("Successfully deleted node {NodeId}", id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to delete node {id}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(id));

        try
        {
            _logger.LogDebug("Deleting relationship {RelationshipId}", id);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.DeleteRelationshipAsync(id, tx, cancellationToken);
                    return true;
                },
                $"Failed to delete relationship {id}",
                _logger);

            _logger.LogDebug("Successfully deleted relationship {RelationshipId}", id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to delete relationship {id}";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }


    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

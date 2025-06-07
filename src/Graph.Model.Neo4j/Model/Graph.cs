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

using Cvoya.Graph.Model.Neo4j.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
internal class Graph : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly GraphQueryProvider _graphQueryProvider;
    private readonly GraphContext _graphContext;

    public Graph(IDriver driver, string databaseName, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(databaseName);
        _logger = loggerFactory?.CreateLogger<Graph>() ?? NullLogger<Graph>.Instance;

        _graphContext = new GraphContext(
            this,
            driver,
            databaseName,
            loggerFactory);

        _graphQueryProvider = new GraphQueryProvider(_graphContext);

        _logger?.LogInformation("Graph initialized for database '{0}'", databaseName);
    }

    /// <inheritdoc />
    public IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : INode
    {
        try
        {
            _logger?.LogDebug("Getting nodes queryable for type {NodeType}", typeof(N).Name);

            var queryContext = new GraphQueryContext { RootType = GraphQueryContext.QueryRootType.Node };
            return new GraphNodeQueryable<N>(_graphQueryProvider, _graphContext, queryContext);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create nodes queryable for type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        try
        {
            _logger?.LogDebug("Getting relationships queryable for type {RelationshipType}", typeof(R).Name);

            var queryContext = new GraphQueryContext { RootType = GraphQueryContext.QueryRootType.Relationship };
            return new GraphRelationshipQueryable<R>(_graphQueryProvider, _graphContext, queryContext);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create relationships queryable for type {typeof(R).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<N> GetNode<N>(string id, IGraphTransaction? transaction = null)
        where N : INode
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Getting node {NodeId} of type {NodeType}", id, typeof(N).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _graphContext.NodeManager.GetNode<N>(id, tx),
                $"Failed to get node {id} of type {typeof(N).Name}");

            _logger?.LogDebug("Successfully retrieved node {NodeId}", id);
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get node {id} of type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<N>> GetNodes<N>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where N : INode
    {
        ArgumentNullException.ThrowIfNull(ids);

        try
        {
            var idList = ids.ToList();
            _logger?.LogDebug("Getting {Count} nodes of type {NodeType}", idList.Count, typeof(N).Name);

            var tasks = ExecuteInTransaction(
                transaction,
                tx => _graphContext.NodeManager.GetNodes<N>(idList, tx),
                $"Failed to get nodes of type {typeof(N).Name}");

            var result = await tasks;

            _logger?.LogDebug("Successfully retrieved {Count} nodes", result.Count());
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get nodes of type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<R> GetRelationship<R>(string id, IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Getting relationship {RelationshipId} of type {RelationshipType}", id, typeof(R).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _graphContext.RelationshipManager.GetRelationship<R>(id, tx),
                $"Failed to get relationship {id} of type {typeof(R).Name}");

            _logger?.LogDebug("Successfully retrieved relationship {RelationshipId}", id);
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get relationship {id} of type {typeof(R).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<R>> GetRelationships<R>(IEnumerable<string> ids, IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        ArgumentNullException.ThrowIfNull(ids);

        try
        {
            var idList = ids.ToList();
            _logger?.LogDebug("Getting {Count} relationships of type {RelationshipType}", idList.Count, typeof(R).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _graphContext.RelationshipManager.GetRelationships<R>(idList, tx),
                $"Failed to get relationships of type {typeof(R).Name}");

            _logger?.LogDebug("Successfully retrieved {Count} relationships", result.Count());
            return result;
        }
        catch (Exception ex) when (ex is not GraphException and not KeyNotFoundException)
        {
            var message = $"Failed to get relationships of type {typeof(R).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateNode<N>(N node, IGraphTransaction? transaction = null)
        where N : INode
    {
        ArgumentNullException.ThrowIfNull(node);

        GraphDataModel.EnforceGraphConstraintsForNode(node);

        try
        {
            _logger?.LogDebug("Creating node of type {NodeType}", typeof(N).Name);

            await ExecuteInTransaction(
                transaction,
                tx => _graphContext.NodeManager.CreateNode(node, tx),
                $"Failed to create node of type {typeof(N).Name}");

            _logger?.LogDebug("Successfully created node {NodeId}", node.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationship<R>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        try
        {
            _logger?.LogDebug("Creating relationship of type {RelationshipType}", typeof(R).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.CreateRelationship(relationship, tx);
                    return true;
                },
                $"Failed to create relationship of type {typeof(R).Name}");

            _logger?.LogDebug("Successfully created relationship {RelationshipId}", relationship.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create relationship of type {typeof(R).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateNode<N>(N node, IGraphTransaction? transaction = null)
        where N : INode
    {
        ArgumentNullException.ThrowIfNull(node);

        GraphDataModel.EnforceGraphConstraintsForNode(node);

        try
        {
            _logger?.LogDebug("Updating node {NodeId} of type {NodeType}", node.Id, typeof(N).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.UpdateNode(node, tx);
                    return true;
                },
            $"Failed to update node {node.Id} of type {typeof(N).Name}");

            _logger?.LogDebug("Successfully updated node {NodeId}", node.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to update node {node.Id} of type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateRelationship<R>(R relationship, IGraphTransaction? transaction = null)
        where R : IRelationship
    {
        ArgumentNullException.ThrowIfNull(relationship);

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        try
        {
            _logger?.LogDebug("Updating relationship {RelationshipId} of type {RelationshipType}", relationship.Id, typeof(R).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.UpdateRelationship(relationship, tx);
                    return true;
                },
            $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}");

            _logger?.LogDebug("Successfully updated relationship {RelationshipId}", relationship.Id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task<IGraphTransaction> BeginTransaction()
    {
        try
        {
            _logger?.LogDebug("Beginning new transaction");

            var session = _graphContext.Driver.AsyncSession(o => o.WithDatabase(_graphContext.DatabaseName));
            var transaction = await session.BeginTransactionAsync();
            var graphTransaction = new GraphTransaction(session, transaction);

            _logger?.LogDebug("Successfully began transaction");
            return graphTransaction;
        }
        catch (Exception ex) when (ex is not GraphTransactionException)
        {
            const string message = "Failed to begin transaction";
            _logger?.LogError(ex, message);
            throw new GraphTransactionException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteNode(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Deleting node {NodeId}", id);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.DeleteNode(id, cascadeDelete, tx);
                    return true;
                },
                $"Failed to delete node {id}");

            _logger?.LogDebug("Successfully deleted node {NodeId}", id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to delete node {id}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationship(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Deleting relationship {RelationshipId}", id);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.DeleteRelationship(id, cascadeDelete, tx);
                    return true;
                },
                $"Failed to delete relationship {id}");

            _logger?.LogDebug("Successfully deleted relationship {RelationshipId}", id);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to delete relationship {id}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }


    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<T> ExecuteInTransaction<T>(
        IGraphTransaction? transaction,
        Func<global::Neo4j.Driver.IAsyncTransaction, Task<T>> function,
    string errorMessage)
    {
        var (session, tx) = await TransactionHelpers.GetOrCreateTransaction(
            _graphContext.Driver,
            _graphContext.DatabaseName, transaction);
        try
        {
            var result = await function(tx);

            if (transaction == null)
            {
                await tx.CommitAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, errorMessage);
            if (transaction == null)
            {
                await tx.RollbackAsync();
            }

            while (ex is GraphException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            throw new GraphException(errorMessage, ex);
        }
        finally
        {
            if (transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }
}

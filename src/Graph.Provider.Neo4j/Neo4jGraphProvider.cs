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

using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Conversion;
using Cvoya.Graph.Provider.Neo4j.Entities;
using Cvoya.Graph.Provider.Neo4j.Linq;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

/// <summary>
/// Neo4j implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
public class Neo4jGraphProvider : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly IDriver _driver;
    private readonly string _databaseName;
    private bool _disposed;

    // Component services
    private readonly Neo4jQueryExecutor _queryExecutor;
    private readonly Neo4jConstraintManager _constraintManager;
    private readonly Neo4jEntityConverter _entityConverter;
    private readonly Neo4jNodeManager _nodeManager;
    private readonly Neo4jRelationshipManager _relationshipManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphProvider"/> class.
    /// </summary>
    /// <param name="uri">The URI of the Neo4j database.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="logger">The logger instance.</param>
    /// <remarks>
    /// The environment variables used for configuration, if not provided, are:
    /// - NEO4J_URI: The URI of the Neo4j database. Default: "bolt://localhost:7687".
    /// - NEO4J_USER: The username for authentication. Default: "neo4j".
    /// - NEO4J_PASSWORD: The password for authentication. Default: "password".
    /// - NEO4J_DATABASE: The name of the database. Default: "neo4j".
    /// </remarks>
    public Neo4jGraphProvider(
        string? uri,
        string? username,
        string? password,
        string? databaseName = "neo4j",
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;

        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        _databaseName = databaseName ?? Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";

        // Create the Neo4j driver
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));

        // Initialize component services
        _queryExecutor = new Neo4jQueryExecutor(_driver, _databaseName, _logger);
        _constraintManager = new Neo4jConstraintManager(_driver, _databaseName, _logger);
        _entityConverter = new Neo4jEntityConverter(_logger);
        _nodeManager = new Neo4jNodeManager(_queryExecutor, _constraintManager, _entityConverter, _logger);
        _relationshipManager = new Neo4jRelationshipManager(_queryExecutor, _constraintManager, _entityConverter, _logger);

        _logger?.LogInformation("Neo4jGraphProvider initialized for database '{Database}' at '{Uri}'", databaseName, uri);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraphProvider"/> class with an existing driver.
    /// </summary>
    /// <param name="driver">The Neo4j driver instance.</param>
    /// <param name="databaseName">The name of the database.</param>
    /// <param name="logger">The logger instance.</param>
    public Neo4jGraphProvider(
        IDriver driver,
        string databaseName = "neo4j",
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(databaseName);

        _logger = logger;
        _driver = driver;
        _databaseName = databaseName;

        // Initialize component services
        _queryExecutor = new Neo4jQueryExecutor(_driver, _databaseName, _logger);
        _constraintManager = new Neo4jConstraintManager(_driver, _databaseName, _logger);
        _entityConverter = new Neo4jEntityConverter(_logger);
        _nodeManager = new Neo4jNodeManager(_queryExecutor, _constraintManager, _entityConverter, _logger);
        _relationshipManager = new Neo4jRelationshipManager(_queryExecutor, _constraintManager, _entityConverter, _logger);

        _logger?.LogInformation("Neo4jGraphProvider initialized with existing driver for database '{Database}'", databaseName);
    }

    #region Neo4j-specific methods

    /// <summary>
    /// Creates the database if it does not already exist.
    /// </summary>
    public async Task CreateDatabaseIfNotExists()
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase("system"));
        await session.RunAsync($"CREATE DATABASE `{_databaseName}` IF NOT EXISTS");
    }

    #endregion

    #region IGraph Implementation

    /// <inheritdoc />
    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger?.LogDebug("Getting nodes queryable for type {NodeType}", typeof(N).Name);

            var queryProvider = new GraphQueryProvider(this, _logger, transaction, typeof(N));

            var context = new GraphQueryContext { RootType = GraphQueryContext.QueryRootType.Node };
            return new GraphQueryable<N>(queryProvider, transaction, context);
        }
        catch (Exception ex) when (ex is not GraphException)
        {
            var message = $"Failed to create nodes queryable for type {typeof(N).Name}";
            _logger?.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger?.LogDebug("Getting relationships queryable for type {RelationshipType}", typeof(R).Name);

            var queryProvider = new GraphQueryProvider(this, _logger, transaction, typeof(R));

            var context = new GraphQueryContext { RootType = GraphQueryContext.QueryRootType.Relationship };
            return new GraphQueryable<R>(queryProvider, transaction, context);
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
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Getting node {NodeId} of type {NodeType}", id, typeof(N).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _nodeManager.GetNode<N>(id, tx),
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
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ids);

        try
        {
            var idList = ids.ToList();
            _logger?.LogDebug("Getting {Count} nodes of type {NodeType}", idList.Count, typeof(N).Name);

            var tasks = ExecuteInTransaction(
                transaction,
                tx => _nodeManager.GetNodes<N>(idList, tx),
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
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Getting relationship {RelationshipId} of type {RelationshipType}", id, typeof(R).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _relationshipManager.GetRelationship<R>(id, tx),
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
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(ids);

        try
        {
            var idList = ids.ToList();
            _logger?.LogDebug("Getting {Count} relationships of type {RelationshipType}", idList.Count, typeof(R).Name);

            var result = await ExecuteInTransaction(
                transaction,
                tx => _relationshipManager.GetRelationships<R>(idList, tx),
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
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(node);

        Helpers.EnforceGraphConstraintsForNode(node);

        try
        {
            _logger?.LogDebug("Creating node of type {NodeType}", typeof(N).Name);

            await ExecuteInTransaction(
                transaction,
                tx => _nodeManager.CreateNode(node, tx),
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
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(relationship);

        Helpers.EnforceGraphConstraintsForRelationship(relationship);

        try
        {
            _logger?.LogDebug("Creating relationship of type {RelationshipType}", typeof(R).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _relationshipManager.CreateRelationship(relationship, tx);
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
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(node);

        Helpers.EnforceGraphConstraintsForNode(node);

        try
        {
            _logger?.LogDebug("Updating node {NodeId} of type {NodeType}", node.Id, typeof(N).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _nodeManager.UpdateNode(node, tx);
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
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(relationship);

        Helpers.EnforceGraphConstraintsForRelationship(relationship);

        try
        {
            _logger?.LogDebug("Updating relationship {RelationshipId} of type {RelationshipType}", relationship.Id, typeof(R).Name);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _relationshipManager.UpdateRelationship(relationship, tx);
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger?.LogDebug("Beginning new transaction");

            var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
            var transaction = await session.BeginTransactionAsync();
            var graphTransaction = new Neo4jGraphTransaction(session, transaction);

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Deleting node {NodeId}", id);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _nodeManager.DeleteNode(id, cascadeDelete, tx);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(id);

        try
        {
            _logger?.LogDebug("Deleting relationship {RelationshipId}", id);

            await ExecuteInTransaction(
                transaction,
                async tx =>
                {
                    await _relationshipManager.DeleteRelationship(id, cascadeDelete, tx);
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

    #endregion

    #region Internal Access Methods for Query Provider

    /// <summary>
    /// Gets the internal query executor for use by the query provider.
    /// </summary>
    internal Neo4jQueryExecutor QueryExecutor => _queryExecutor;

    /// <summary>
    /// Gets the internal entity converter for use by the query provider.
    /// </summary>
    internal Neo4jEntityConverter EntityConverter => _entityConverter;

    /// <summary>
    /// Gets the internal constraint manager for use by the query provider.
    /// </summary>
    internal Neo4jConstraintManager ConstraintManager => _constraintManager;

    /// <summary>
    /// Gets the database name for use by the query provider.
    /// </summary>
    internal string DatabaseName => _databaseName;

    /// <summary>
    /// Gets the driver instance for use by the query provider.
    /// </summary>
    internal IDriver Driver => _driver;

    /// <summary>
    /// Extracts the underlying Neo4j transaction from a graph transaction.
    /// </summary>
    /// <param name="transaction">The graph transaction to extract from</param>
    /// <returns>The underlying Neo4j transaction or null</returns>
    private static IAsyncTransaction? ExtractNeo4jTransaction(IGraphTransaction? transaction)
    {
        return (transaction as Neo4jGraphTransaction)?.GetTransaction();
    }

    /// <summary>
    /// Gets or creates a Neo4j transaction for query execution.
    /// </summary>
    /// <param name="transaction">The graph transaction to use or null to create a new one</param>
    /// <returns>A tuple containing the session and transaction</returns>
    internal async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
    {
        return await _queryExecutor.GetOrCreateTransaction(transaction);
    }

    #endregion

    #region IAsyncDisposable Implementation

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            _logger?.LogDebug("Disposing Neo4jGraphProvider");

            await _driver.DisposeAsync();

            _logger?.LogDebug("Neo4jGraphProvider disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during Neo4jGraphProvider disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    #endregion

    #region private

    private async Task<T> ExecuteInTransaction<T>(
        IGraphTransaction? transaction,
        Func<IAsyncTransaction, Task<T>> function,
    string errorMessage)
    {
        var (session, tx) = await _queryExecutor.GetOrCreateTransaction(transaction);
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
    #endregion
}

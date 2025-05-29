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
/// Neo4j implementation of the IGraph interface using a modular design.
/// </summary>
public class Neo4jGraphProvider : IGraph
{
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly IDriver _driver;
    private readonly string _databaseName;

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
        string? uri = null,
        string? username = null,
        string? password = null,
        string? databaseName = null,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
        uri ??= Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        username ??= Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        password ??= Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";
        _databaseName = databaseName ?? Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "neo4j";

        _driver = username is null
            ? GraphDatabase.Driver(uri)
            : GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));

        // Initialize component services
        _entityConverter = new Neo4jEntityConverter(logger);
        _queryExecutor = new Neo4jQueryExecutor(_driver, _databaseName, logger);
        _constraintManager = new Neo4jConstraintManager(_driver, _databaseName, logger);
        _nodeManager = new Neo4jNodeManager(_queryExecutor, _constraintManager, _entityConverter, logger);
        _relationshipManager = new Neo4jRelationshipManager(_queryExecutor, _constraintManager, _entityConverter, logger);
    }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>A new graph transaction</returns>
    public async Task<IGraphTransaction> BeginTransaction()
    {
        var session = _driver.AsyncSession(builder => builder.WithDatabase(_databaseName));
        var transaction = await session.BeginTransactionAsync();
        return new Neo4jGraphTransaction(session, transaction);
    }

    /// <inheritdoc />
    public IQueryable<N> Nodes<N>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        var provider = new Neo4jQueryProvider(this, options, _logger, transaction as Neo4jGraphTransaction);
        var query = new Neo4jQueryable<N>(provider, options, transaction);
        return query;
    }

    /// <inheritdoc />
    public IQueryable<R> Relationships<R>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        var provider = new Neo4jQueryProvider(this, options, _logger, transaction as Neo4jGraphTransaction);
        var query = new Neo4jQueryable<R>(provider, options, transaction);
        return query;
    }

    /// <inheritdoc />
    public Task<N> GetNode<N>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ArgumentNullException.ThrowIfNull(id);

        // Validate the node type
        Helpers.EnforceGraphConstraintsForNodeType<N>();

        return ExecuteInTransaction(transaction, async tx =>
        {
            return await _nodeManager.GetNode<N>(id, options, tx);
        }, $"Failed to get node with ID: {id}");
    }

    /// <inheritdoc />
    public Task<IEnumerable<N>> GetNodes<N>(
        IEnumerable<string> ids,
        GraphOperationOptions options = default,
        IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ArgumentNullException.ThrowIfNull(ids);

        // Validate the node type
        Helpers.EnforceGraphConstraintsForNodeType<N>();

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return Task.FromResult(Enumerable.Empty<N>());
        }

        return ExecuteInTransaction(transaction, async tx =>
        {
            return await _nodeManager.GetNodes<N>(idList, options, tx);
        }, "Failed to get nodes");
    }

    /// <inheritdoc />
    public Task<R> GetRelationship<R>(
        string id,
        GraphOperationOptions options = default,
        IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ArgumentNullException.ThrowIfNull(id);

        // Validate the relationship type
        Helpers.EnforceGraphConstraintsForRelationshipType<R>();

        return ExecuteInTransaction(transaction, async tx =>
        {
            return await _relationshipManager.GetRelationship<R>(id, options, tx);
        }, $"Failed to get relationship with ID: {id}");
    }

    /// <inheritdoc />
    public Task<IEnumerable<R>> GetRelationships<R>(
        IEnumerable<string> ids,
        GraphOperationOptions options = default,
        IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        ArgumentNullException.ThrowIfNull(ids);

        // Validate the relationship type
        Helpers.EnforceGraphConstraintsForRelationshipType<R>();

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return Task.FromResult(Enumerable.Empty<R>());
        }

        return ExecuteInTransaction(transaction, async tx =>
        {
            return await _relationshipManager.GetRelationships<R>(idList, options, tx);
        }, $"Failed to get relationships");
    }

    /// <inheritdoc />
    public Task CreateNode<N>(N node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        ArgumentNullException.ThrowIfNull(node);

        // Validate the node        
        Helpers.EnforceGraphConstraintsForNode(node);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Create the node
            await _nodeManager.CreateNode(parentId: null, node: node, options, tx: tx);
            return true;
        }, $"Failed to create node");
    }

    /// <inheritdoc />
    public Task CreateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        // Validate the relationship        
        Helpers.EnforceGraphConstraintsForRelationship(relationship);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Create the relationship
            await _relationshipManager.CreateRelationship(relationship, options, tx);
            return true;
        }, $"Failed to create relationship");
    }

    /// <inheritdoc />
    public Task UpdateNode<N>(N node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, Cvoya.Graph.Model.INode, new()
    {
        Helpers.EnforceGraphConstraintsForNode(node);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Update the node
            await _nodeManager.UpdateNode(node, options, tx);
            return true;
        }, $"Failed to update node");
    }

    /// <inheritdoc />
    public Task UpdateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, Cvoya.Graph.Model.IRelationship, new()
    {
        Helpers.EnforceGraphConstraintsForRelationship(relationship);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Update the node
            await _relationshipManager.UpdateRelationship(relationship, options, tx);
            return true;
        }, $"Failed to update relationship");
    }

    /// <inheritdoc />
    public Task DeleteRelationship(string relationshipId, GraphOperationOptions options, IGraphTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(relationshipId);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Delete the relationship
            await Neo4jRelationshipManager.DeleteRelationship(relationshipId, options, tx);
            return true;
        }, $"Failed to delete relationship");
    }

    /// <inheritdoc />
    public Task DeleteNode(string nodeId, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(nodeId);

        return ExecuteInTransaction<bool>(transaction, async tx =>
        {
            // Delete the node
            await _nodeManager.DeleteNode(nodeId, options, tx);
            return true;
        }, $"Failed to delete node");
    }

    /// <summary>
    /// Gets or creates a Neo4j transaction from a graph transaction.
    /// </summary>
    internal async Task<(IAsyncSession, IAsyncTransaction)> GetOrCreateTransaction(IGraphTransaction? transaction)
    {
        return await _queryExecutor.GetOrCreateTransaction(transaction);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _driver.DisposeAsync();
        GC.SuppressFinalize(this);
    }

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
}

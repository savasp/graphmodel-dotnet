// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Core;

using Cvoya.Graph.Neo4j.Querying.Linq.Providers;
using Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Neo4j implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
/// <remarks>
/// Instances are created and owned by <see cref="Neo4jGraphStore"/>. Dispose the store to release
/// provider-owned resources; this graph object does not own the Neo4j driver lifetime.
/// </remarks>
internal class Neo4jGraph : IGraph
{
    private readonly ILogger _logger;
    private readonly GraphContext _graphContext;
    private readonly SchemaRegistry _schemaRegistry;

    // Keep a reference to the graph store to ensure
    // that it's not garbage collected
    private readonly Neo4jGraphStore _graphStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jGraph"/> class.
    /// </summary>
    public Neo4jGraph(Neo4jGraphStore store, string databaseName, SchemaRegistry schemaRegistry, ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<Neo4jGraph>() ?? NullLogger<Neo4jGraph>.Instance;
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _graphStore = store ?? throw new ArgumentNullException(nameof(store));

        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        _graphContext = new GraphContext(
            this,
            () => _graphStore.Driver,
            databaseName,
            loggerFactory,
            _schemaRegistry);

        // Don't initialize schema registry here - let it be initialized lazily on first use
        // This avoids double initialization and concurrency issues
        _logger.LogInformation("Graph initialized for database '{DatabaseName}'", databaseName);
    }

    public SchemaRegistry SchemaRegistry => _schemaRegistry;

    /// <inheritdoc />
    public async Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Beginning new transaction");

            var graphTransaction = new GraphTransaction(_graphContext);
            await graphTransaction.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully began transaction");
            return graphTransaction;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            const string message = "Failed to begin transaction";
            _logger.LogError(ex, message);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : class, Graph.INode
    {
        _logger.LogDebug("Building nodes queryable for type {NodeType}", typeof(N).Name);

        // Building a queryable performs no I/O. When no transaction is provided, execution time
        // (GraphQueryProvider.ExecuteAsync -> TransactionHelpers.ExecuteInTransactionAsync)
        // creates a per-execution transaction with proper lifecycle management, which prevents
        // session/connection leaks from long-lived queryables.
        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphNodeQueryable<N>(provider, neo4jTx, _graphContext);
    }

    /// <inheritdoc />
    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : class, Graph.IRelationship
    {
        _logger.LogDebug("Building relationships queryable for type {RelationshipType}", typeof(R).Name);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphRelationshipQueryable<R>(provider, _graphContext, neo4jTx);
    }

    /// <inheritdoc />
    public async Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : class, Graph.INode
    {
        var query = Nodes<N>(transaction)
            .Where(n => n.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Node with ID {id} not found");
    }

    /// <inheritdoc />
    public async Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : class, Graph.IRelationship
    {
        var query = Relationships<R>(transaction)
            .Where(r => r.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Relationship with ID {id} not found");
    }

    /// <summary>
    /// Converts a public <see cref="IGraphTransaction"/> to the internal <see cref="GraphTransaction"/>
    /// used by queryable construction, or <see langword="null"/> if none was given (in which case
    /// execution creates a per-execution transaction).
    /// </summary>
    private static GraphTransaction? ToNeo4jTransaction(IGraphTransaction? transaction) => transaction switch
    {
        null => null,
        GraphTransaction neo4jTx => neo4jTx,
        _ => throw new GraphException(
            "The given transaction is not a valid Neo4j transaction. You need to use Neo4jStore.Graph.GetTransactionAsync() to create a transaction.")
    };

    /// <inheritdoc />
    public async Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : class, Graph.INode
    {
        if (node is null)
            throw new ArgumentException("Node cannot be null.", nameof(node));

        if (string.IsNullOrEmpty(node.Id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Creating node of type {NodeType}", typeof(N).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data operations)
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.NodeManager.CreateNodeAsync(node, tx, cancellationToken),
                $"Failed to create node of type {typeof(N).Name}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully created node {NodeId}", node.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            _logger.LogError(ex, "Failed to create node of type {NodeType}", typeof(N).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : class, Graph.IRelationship
    {
        if (relationship is null)
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Creating relationship of type {RelationshipType}", typeof(R).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data operations)
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.CreateRelationshipAsync(relationship, tx, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                $"Failed to create relationship of type {typeof(R).Name}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully created relationship {RelationshipId}", relationship.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create relationship of type {typeof(R).Name}";
            _logger.LogError(ex, "Failed to create relationship of type {RelationshipType}", typeof(R).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : class, Graph.INode
    {
        if (node is null)
            throw new ArgumentException("Node cannot be null.", nameof(node));

        if (string.IsNullOrEmpty(node.Id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));

        GraphDataModel.EnforceGraphConstraintsForNode(node);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Updating node {NodeId} of type {NodeType}", node.Id, typeof(N).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data
            // operations). The update path validates properties against the schema registry, so
            // this must not depend on a prior create having initialized it (#227).
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.UpdateNodeAsync(node, tx, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                $"Failed to update node {node.Id} of type {typeof(N).Name}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully updated node {NodeId}", node.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to update node {node.Id} of type {typeof(N).Name}";
            _logger.LogError(ex, "Failed to update node {NodeId} of type {NodeType}", node.Id, typeof(N).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where R : class, Graph.IRelationship
    {
        if (relationship is null)
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));

        if (string.IsNullOrEmpty(relationship.Id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Updating relationship {RelationshipId} of type {RelationshipType}", relationship.Id, typeof(R).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data
            // operations). The update path validates properties against the schema registry, so
            // this must not depend on a prior create having initialized it (#227).
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.UpdateRelationshipAsync(relationship, tx, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully updated relationship {RelationshipId}", relationship.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}";
            _logger.LogError(ex, "Failed to update relationship {RelationshipId} of type {RelationshipType}", relationship.Id, typeof(R).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Deleting node {NodeId}", id);

            // Delete-by-ID needs the complete registered-label set to recognize legacy nodes
            // written before EntityKind was stored. Initialize before opening the data
            // transaction so a cold registry cannot turn an existing node into not-found (#239).
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.NodeManager.DeleteNodeAsync(id, tx, cascadeDelete, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                $"Failed to delete node {id}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully deleted node {NodeId}", id);

        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete node {id}";
            _logger.LogError(ex, "Failed to delete node {NodeId}", id);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebug("Deleting relationship {RelationshipId}", id);

            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    await _graphContext.RelationshipManager.DeleteRelationshipAsync(id, tx, cancellationToken).ConfigureAwait(false);
                    return true;
                },
                $"Failed to delete relationship {id}",
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully deleted relationship {RelationshipId}", id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete relationship {id}";
            _logger.LogError(ex, "Failed to delete relationship {RelationshipId}", id);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }
            throw new GraphException(message, ex);
        }
    }

    // Dynamic entity methods

    /// <inheritdoc />
    public IGraphQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null)
    {
        _logger.LogDebug("Building dynamic nodes queryable");

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphNodeQueryable<DynamicNode>(provider, neo4jTx, _graphContext);
    }

    /// <inheritdoc />
    public IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null)
    {
        _logger.LogDebug("Building dynamic relationships queryable");

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphRelationshipQueryable<DynamicRelationship>(provider, _graphContext, neo4jTx);
    }

    /// <inheritdoc />
    public async Task<DynamicNode> GetDynamicNodeAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var query = DynamicNodes(transaction)
            .Where(n => n.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Dynamic node with ID {id} not found");
    }

    /// <inheritdoc />
    public async Task<DynamicRelationship> GetDynamicRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var query = DynamicRelationships(transaction)
            .Where(r => r.Id == id);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Dynamic relationship with ID {id} not found");
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.IEntity> Search(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebug("Building full text search queryable; query length: {QueryLength}", query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateSearchQueryable<Graph.IEntity>(provider, neo4jTx, _graphContext, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.INode> SearchNodes(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebug("Building full text search queryable for nodes; query length: {QueryLength}", query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateNodeSearchQueryable<Graph.INode>(provider, neo4jTx, _graphContext, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebug("Building full text search queryable for relationships; query length: {QueryLength}", query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateRelationshipSearchQueryable<Graph.IRelationship>(provider, neo4jTx, _graphContext, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.INode
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebug(
            "Building full text search queryable for nodes of type {NodeType}; query length: {QueryLength}",
            typeof(T).Name,
            query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateNodeSearchQueryable<T>(provider, neo4jTx, _graphContext, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebug(
            "Building full text search queryable for relationships of type {RelationshipType}; query length: {QueryLength}",
            typeof(T).Name,
            query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateRelationshipSearchQueryable<T>(provider, neo4jTx, _graphContext, query);
    }

    /// <inheritdoc />
    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recreating indexes for Neo4j graph");
            await _graphContext.SchemaManager.RecreateIndexesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Index recreation completed successfully");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate indexes");
            throw new GraphException("Failed to recreate indexes", ex);
        }
    }

    internal global::Neo4j.Driver.IDriver Driver => _graphStore.Driver;
}

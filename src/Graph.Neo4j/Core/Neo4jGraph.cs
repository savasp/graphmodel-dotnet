// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Core;

using Cvoya.Graph.Neo4j.Querying.Linq.Providers;
using Cvoya.Graph.Neo4j.Querying.Linq.Queryables;
using Cvoya.Graph.Querying.Commands;
using Cvoya.Graph.Querying.Linq;

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
        _logger.LogInformationNeo4jGraph51(databaseName);
    }

    public SchemaRegistry SchemaRegistry => _schemaRegistry;

    /// <inheritdoc />
    public async Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebugNeo4jGraph63();

            var graphTransaction = new GraphTransaction(_graphContext);
            await graphTransaction.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebugNeo4jGraph68();
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
            _logger.LogErrorNeo4jGraph82(ex);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : class, Graph.INode
    {
        _logger.LogDebugNeo4jGraph91(typeof(N).Name);

        // Building a queryable performs no I/O. When no transaction is provided, execution time
        // (GraphQueryProvider.ExecuteAsync -> TransactionHelpers.ExecuteInTransactionAsync)
        // creates a per-execution transaction with proper lifecycle management, which prevents
        // session/connection leaks from long-lived queryables.
        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphNodeQueryable<N>(provider);
    }

    /// <inheritdoc />
    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : class, Graph.IRelationship
    {
        _logger.LogDebugNeo4jGraph106(typeof(R).Name);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphRelationshipQueryable<R>(provider);
    }

    /// <summary>
    /// Converts a public <see cref="IGraphTransaction"/> to the internal <see cref="GraphTransaction"/>
    /// used by queryable construction, or <see langword="null"/> if none was given (in which case
    /// execution creates a per-execution transaction). Rejects transactions from another provider
    /// and transactions owned by a different Neo4j graph (reference identity, not settings).
    /// </summary>
    private GraphTransaction? ToNeo4jTransaction(IGraphTransaction? transaction) => transaction switch
    {
        null => null,
        GraphTransaction neo4jTx when neo4jTx.BelongsTo(_graphContext) => neo4jTx,
        GraphTransaction => throw new GraphException(
            "The given transaction was created by a different Neo4j graph store. A transaction can only be used with the graph that created it."),
        _ => throw new GraphException(
            "The given transaction is not a valid Neo4j transaction. Use Neo4jGraphStore.Graph.GetTransactionAsync() to create one.")
    };

    /// <summary>
    /// Validates a caller-supplied transaction at a CRUD entry point: foreign-provider and
    /// wrong-graph transactions are rejected here, before schema initialization or any store
    /// work, and without touching the caller-owned transaction's lifecycle.
    /// </summary>
    private void EnsureOwnedTransaction(IGraphTransaction? transaction) => _ = ToNeo4jTransaction(transaction);

    /// <inheritdoc />
    public async Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where N : class, Graph.INode
    {
        if (node is null)
            throw new ArgumentException("Node cannot be null.", nameof(node));

        cancellationToken.ThrowIfCancellationRequested();
        EnsureOwnedTransaction(transaction);

        try
        {
            _logger.LogDebugNeo4jGraph162(typeof(N).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data operations)
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.NodeManager.CreateNodeAsync(node, tx, cancellationToken),
                $"Failed to create node of type {typeof(N).Name}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            _logger.LogErrorNeo4jGraph183(ex, typeof(N).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
        => GraphCommandExtensions.CreateNewAsync(
            this,
            source,
            relationship,
            target,
            direction,
            GraphRelationshipCreationMode.Standard,
            transaction,
            cancellationToken);

    // Dynamic entity methods

    /// <inheritdoc />
    public IGraphQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null)
    {
        _logger.LogDebugNeo4jGraph454();

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphNodeQueryable<DynamicNode>(provider);
    }

    /// <inheritdoc />
    public IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null)
    {
        _logger.LogDebugNeo4jGraph464();

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return new GraphRelationshipQueryable<DynamicRelationship>(provider);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.IEntity> Search(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebugNeo4jGraph496(query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateSearchQueryable<Graph.IEntity>(provider, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.INode> SearchNodes(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebugNeo4jGraph508(query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateNodeSearchQueryable<Graph.INode>(provider, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebugNeo4jGraph520(query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateRelationshipSearchQueryable<Graph.IRelationship>(provider, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.INode
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebugNeo4jGraph532(typeof(T).Name, query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateNodeSearchQueryable<T>(provider, query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        _logger.LogDebugNeo4jGraph547(typeof(T).Name, query.Length);

        var neo4jTx = ToNeo4jTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, neo4jTx, isReadOnly: true);
        return FullTextSearchQueryableFactory.CreateRelationshipSearchQueryable<T>(provider, query);
    }

    /// <inheritdoc />
    public async Task RecreateManagedIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformationNeo4jGraph562();
            await _graphContext.SchemaManager.RecreateManagedIndexesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformationNeo4jGraph564();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorNeo4jGraph572(ex);
            throw new GraphException("Failed to recreate managed indexes", ex);
        }
    }

    internal global::Neo4j.Driver.IDriver Driver => _graphStore.Driver;

    /// <summary>
    /// Gets the graph context backing this graph instance. Exposed internally so tests can reach
    /// provider-internal managers and construct transactions (e.g. an explicit read-only
    /// <see cref="GraphTransaction"/>) that belong to this graph, without duplicating its wiring.
    /// </summary>
    internal GraphContext Context => _graphContext;
}

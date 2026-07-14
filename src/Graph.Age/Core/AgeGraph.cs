// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Core;

using Cvoya.Graph.Age.Querying;
using Cvoya.Graph.Age.Querying.Linq.Providers;
using Cvoya.Graph.Querying.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

/// <summary>
/// Age implementation of the IGraph interface using a modular design with IGraphQueryable support.
/// </summary>
/// <remarks>
/// Instances are created and owned by <see cref="AgeGraphStore"/>. Dispose the store to release
/// provider-owned resources; this graph object does not own the PostgreSQL data-source lifetime.
/// </remarks>
internal class AgeGraph : IGraph
{
    private readonly ILogger _logger;
    private readonly AgeGraphContext _graphContext;
    private readonly SchemaRegistry _schemaRegistry;

    // Keep a reference to the graph store to ensure
    // that it's not garbage collected
    private readonly AgeGraphStore _graphStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgeGraph"/> class.
    /// </summary>
    public AgeGraph(AgeGraphStore store, string graphName, SchemaRegistry schemaRegistry, ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<AgeGraph>() ?? NullLogger<AgeGraph>.Instance;
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _graphStore = store ?? throw new ArgumentNullException(nameof(store));

        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

        _graphContext = new AgeGraphContext(
            this,
            _graphStore,
            loggerFactory,
            _schemaRegistry);

        // Don't initialize schema registry here - let it be initialized lazily on first use
        // This avoids double initialization and concurrency issues
        _logger.LogInformationAgeGraph49(graphName);
    }

    public SchemaRegistry SchemaRegistry => _schemaRegistry;

    /// <inheritdoc />
    public async Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _logger.LogDebugAgeGraph61();

            var graphTransaction = new AgeGraphTransaction(_graphContext);
            await graphTransaction.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph66();
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
        catch (NpgsqlException ex)
        {
            const string message = "Failed to begin transaction";
            _logger.LogErrorAgeGraph80(ex);
            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null)
        where N : class, Graph.INode
    {
        _logger.LogDebugAgeGraph89(typeof(N).Name);

        // Building a queryable performs no I/O. When no transaction is provided, execution time
        // (GraphQueryProvider.ExecuteAsync -> TransactionHelpers.ExecuteInTransactionAsync)
        // creates a per-execution transaction with proper lifecycle management, which prevents
        // session/connection leaks from long-lived queryables.
        var ageTransaction = ToAgeTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, ageTransaction, isReadOnly: true);
        return new GraphNodeQueryable<N>(provider);
    }

    /// <inheritdoc />
    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null)
        where R : class, Graph.IRelationship
    {
        _logger.LogDebugAgeGraph104(typeof(R).Name);

        var ageTransaction = ToAgeTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, ageTransaction, isReadOnly: true);
        return new GraphRelationshipQueryable<R>(provider);
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
    /// Converts a public <see cref="IGraphTransaction"/> to the internal <see cref="AgeGraphTransaction"/>
    /// used by queryable construction, or <see langword="null"/> if none was given (in which case
    /// execution creates a per-execution transaction).
    /// </summary>
    private static AgeGraphTransaction? ToAgeTransaction(IGraphTransaction? transaction) => transaction switch
    {
        null => null,
        AgeGraphTransaction ageTransaction => ageTransaction,
        _ => throw new GraphException(
            "The given transaction is not a valid AGE transaction. Use AgeGraphStore.Graph.GetTransactionAsync() to create it.")
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
            _logger.LogDebugAgeGraph160(typeof(N).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data operations)
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            await TransactionHelpers.ExecuteInTransactionAsync(
                graphContext: _graphContext,
                transaction: transaction,
                tx => _graphContext.NodeManager.CreateNodeAsync(node, tx, cancellationToken),
                $"Failed to create node of type {typeof(N).Name}",
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph173(node.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create node of type {typeof(N).Name}";
            _logger.LogErrorAgeGraph182(ex, typeof(N).Name);

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
            _logger.LogDebugAgeGraph208(typeof(R).Name);

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
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph225(relationship.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create relationship of type {typeof(R).Name}";
            _logger.LogErrorAgeGraph234(ex, typeof(R).Name);

            if (ex is GraphException)
            {
                // If it's already a GraphException, rethrow it
                throw;
            }

            throw new GraphException(message, ex);
        }
    }

    /// <inheritdoc />
    public async Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        GraphOperationOptions? options = null,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, Graph.INode
        where TRelationship : class, Graph.IRelationship
        where TTarget : class, Graph.INode
    {
        SubgraphArguments.Validate(source, relationship, target);

        cancellationToken.ThrowIfCancellationRequested();

        var createMissingEndpoints = options?.CreateMissingEndpoints ?? false;

        try
        {
            _logger.LogDebugAgeGraph208(typeof(TRelationship).Name);

            // Ensure schema is created before any transaction (to avoid mixing schema and data operations)
            await _graphContext.SchemaManager.InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);

            // Both endpoint nodes (with their complex-property subtrees) and the edge are created
            // within one AGE transaction, so any failure rolls back the whole subgraph.
            await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                async tx =>
                {
                    // The provider implementation is intentionally multi-statement. When the caller
                    // owns the surrounding transaction, isolate this operation behind a savepoint so
                    // a later endpoint/relationship failure cannot leave partial writes that the caller
                    // could subsequently commit.
                    var savepoint = transaction is null ? null : $"cvoya_subgraph_{Guid.NewGuid():N}";
                    if (savepoint is not null)
                    {
                        await tx.DbTransaction.SaveAsync(savepoint, cancellationToken).ConfigureAwait(false);
                    }

                    try
                    {
                        if (createMissingEndpoints)
                        {
                            await _graphContext.NodeManager.CreateNodeIfMissingAsync(source, tx, cancellationToken).ConfigureAwait(false);
                            await _graphContext.NodeManager.CreateNodeIfMissingAsync(target, tx, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await _graphContext.NodeManager.CreateNodeAsync(source, tx, cancellationToken).ConfigureAwait(false);
                            await _graphContext.NodeManager.CreateNodeAsync(target, tx, cancellationToken).ConfigureAwait(false);
                        }

                        await _graphContext.RelationshipManager.CreateRelationshipAsync(relationship, tx, cancellationToken).ConfigureAwait(false);

                        if (savepoint is not null)
                        {
                            await tx.DbTransaction.ReleaseAsync(savepoint, cancellationToken).ConfigureAwait(false);
                        }

                        return true;
                    }
                    catch (Exception operationException) when (savepoint is not null)
                    {
                        try
                        {
                            await tx.DbTransaction.RollbackAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                            await tx.DbTransaction.ReleaseAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception rollbackException) when (rollbackException is NpgsqlException or InvalidOperationException)
                        {
                            throw new GraphException(
                                "Failed to restore the caller transaction after subgraph creation failed.",
                                new AggregateException(operationException, rollbackException));
                        }

                        throw;
                    }
                },
                $"Failed to create subgraph for relationship of type {typeof(TRelationship).Name}",
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to create subgraph for relationship of type {typeof(TRelationship).Name}";
            _logger.LogErrorAgeGraph234(ex, typeof(TRelationship).Name);

            if (ex is GraphException)
            {
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
            _logger.LogDebugAgeGraph262(node.Id, typeof(N).Name);

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
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph281(node.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to update node {node.Id} of type {typeof(N).Name}";
            _logger.LogErrorAgeGraph290(ex, node.Id, typeof(N).Name);

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
            _logger.LogDebugAgeGraph318(relationship.Id, typeof(R).Name);

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
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph337(relationship.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}";
            _logger.LogErrorAgeGraph346(ex, relationship.Id, typeof(R).Name);

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
            _logger.LogDebugAgeGraph368(id);

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
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebugAgeGraph387(id);

        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete node {id}";
            _logger.LogErrorAgeGraph397(ex, id);

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
            _logger.LogDebugAgeGraph418(id);

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

            _logger.LogDebugAgeGraph432(id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Failed to delete relationship {id}";
            _logger.LogErrorAgeGraph441(ex, id);

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
        _logger.LogDebugAgeGraph457();

        var ageTransaction = ToAgeTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, ageTransaction, isReadOnly: true);
        return new GraphNodeQueryable<DynamicNode>(provider);
    }

    /// <inheritdoc />
    public IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null)
    {
        _logger.LogDebugAgeGraph467();

        var ageTransaction = ToAgeTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, ageTransaction, isReadOnly: true);
        return new GraphRelationshipQueryable<DynamicRelationship>(provider);
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

        var ageTransaction = ToAgeTransaction(transaction);
        var provider = new GraphQueryProvider(_graphContext, ageTransaction, isReadOnly: true);
        var nodeSource = ((IQueryable)new GraphNodeQueryable<Graph.INode>(provider)).Expression;
        var relationshipSource = ((IQueryable)new GraphRelationshipQueryable<Graph.IRelationship>(provider)).Expression;
        var searchRoot = new AgeMixedSearchRootExpression(query, nodeSource, relationshipSource);
        return new GraphQueryable<Graph.IEntity>(provider, searchRoot);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.INode> SearchNodes(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        return Nodes<Graph.INode>(transaction).Search(query);
    }

    /// <inheritdoc />
    public IGraphQueryable<Graph.IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        return Relationships<Graph.IRelationship>(transaction).Search(query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.INode
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        return Nodes<T>(transaction).Search(query);
    }

    /// <inheritdoc />
    public IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null) where T : class, Graph.IRelationship
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));

        return Relationships<T>(transaction).Search(query);
    }

    /// <inheritdoc />
    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _logger.LogInformationAgeGraph539();
            await _graphContext.SchemaManager.RecreateIndexesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformationAgeGraph541();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GraphException ex)
        {
            // Schema initialization is pure in-memory reflection scanning (no Npgsql call in
            // this path); the only failure it raises is a label-collision GraphException.
            _logger.LogErrorAgeGraph551(ex);
            throw new GraphException("Failed to recreate indexes", ex);
        }
    }

}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Cvoya.Graph.InMemory.Querying;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The in-memory <see cref="IGraph"/>: CRUD over decomposed serialized snapshots, buffered-write
/// transactions, and LINQ execution through the shared provider-neutral query model. Instances
/// are created and owned by <see cref="InMemoryGraphStore"/>.
/// </summary>
internal sealed class InMemoryGraph : IGraph
{
    private readonly InMemoryStore _store;
    private readonly SchemaRegistry _schemaRegistry;
    private readonly EntityFactory _entityFactory;
    private readonly EntityReader _reader;
    private readonly EntityValidator _validator;
    private readonly ILogger _logger;

    public InMemoryGraph(InMemoryStore store, SchemaRegistry schemaRegistry, ILoggerFactory? loggerFactory)
    {
        _store = store;
        _schemaRegistry = schemaRegistry;
        _entityFactory = new EntityFactory(loggerFactory);
        _reader = new EntityReader(_entityFactory);
        _validator = new EntityValidator(schemaRegistry);
        _logger = loggerFactory?.CreateLogger<InMemoryGraph>() ?? NullLogger<InMemoryGraph>.Instance;
    }

    public SchemaRegistry SchemaRegistry => _schemaRegistry;

    public Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IGraphTransaction>(new InMemoryTransaction(_store));
    }

    public IGraphQueryable<DynamicNode> DynamicNodes(IGraphTransaction? transaction = null) =>
        Query<DynamicNode>(transaction);

    public IGraphQueryable<DynamicRelationship> DynamicRelationships(IGraphTransaction? transaction = null) =>
        Query<DynamicRelationship>(transaction);

    public IGraphQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : class, INode =>
        Query<N>(transaction);

    public IGraphQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : class, IRelationship =>
        Query<R>(transaction);

    public async Task<DynamicNode> GetDynamicNodeAsync(
        string id,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                var record = FindNodeRecord(tx.View, id, typeof(DynamicNode))
                    ?? throw new EntityNotFoundException($"Node with ID {id} not found");
                return _reader.MaterializeNode<DynamicNode>(record, tx.View);
            },
            $"Failed to get dynamic node {id}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DynamicRelationship> GetDynamicRelationshipAsync(
        string id,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                var record = FindRelationshipRecord(tx.View, id, typeof(DynamicRelationship))
                    ?? throw new EntityNotFoundException($"Relationship with ID {id} not found");
                return (DynamicRelationship)_reader.MaterializeRelationship(record, typeof(DynamicRelationship));
            },
            $"Failed to get dynamic relationship {id}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<N> GetNodeAsync<N>(
        string id,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where N : class, INode
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                var record = FindNodeRecord(tx.View, id, typeof(N))
                    ?? throw new EntityNotFoundException($"Node with ID {id} not found");
                return _reader.MaterializeNode<N>(record, tx.View);
            },
            $"Failed to get node {id} of type {typeof(N).Name}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<R> GetRelationshipAsync<R>(
        string id,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where R : class, IRelationship
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                var record = FindRelationshipRecord(tx.View, id, typeof(R))
                    ?? throw new EntityNotFoundException($"Relationship with ID {id} not found");
                return (R)_reader.MaterializeRelationship(record, typeof(R));
            },
            $"Failed to get relationship {id} of type {typeof(R).Name}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateNodeAsync<N>(
        N node,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where N : class, INode
    {
        if (node is null)
        {
            throw new ArgumentException("Node cannot be null.", nameof(node));
        }

        if (string.IsNullOrEmpty(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        GraphDataModel.EnforceGraphConstraintsForNode(node);
        _validator.ValidateNode(node);

        var decomposed = EntityWriter.DecomposeNode(_entityFactory.Serialize(node));
        var constraints = ConstraintChecker.From(_schemaRegistry.GetNodeSchema(decomposed.Node.Label));
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckNode(state, decomposed.Node, constraints);
                    }

                    return state.AddNode(decomposed.Node, decomposed.ComplexValueNodes, decomposed.ComplexEdges);
                });
                return true;
            },
            $"Failed to create node of type {typeof(N).Name}",
            cancellationToken).ConfigureAwait(false);

        RuntimeMetadata.PopulateNodeLabels(node, decomposed.Node.Labels);
        _logger.LogDebugInMemoryGraph178(node.Id, typeof(N).Name);
    }

    public async Task CreateRelationshipAsync<R>(
        R relationship,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where R : class, IRelationship
    {
        if (relationship is null)
        {
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));
        }

        if (string.IsNullOrEmpty(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);
        _validator.ValidateRelationship(relationship);

        var record = EntityWriter.DecomposeRelationship(_entityFactory.Serialize(relationship), relationship);
        var constraints = ConstraintChecker.From(_schemaRegistry.GetRelationshipSchema(record.Type));
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckRelationship(state, record, constraints);
                    }

                    return state.AddRelationship(record);
                });
                return true;
            },
            $"Failed to create relationship of type {typeof(R).Name}",
            cancellationToken).ConfigureAwait(false);

        RuntimeMetadata.PopulateRelationshipType(relationship, record.Type);
        _logger.LogDebugInMemoryGraph225(relationship.Id, typeof(R).Name);
    }

    public async Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        GraphOperationOptions? options = null,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        SubgraphArguments.Validate(source, relationship, target);

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        GraphDataModel.EnforceGraphConstraintsForNode(source);
        GraphDataModel.EnforceGraphConstraintsForNode(target);
        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);
        _validator.ValidateNode(source);
        _validator.ValidateNode(target);
        _validator.ValidateRelationship(relationship);

        var createMissingEndpoints = options?.CreateMissingEndpoints ?? false;

        var decomposedSource = EntityWriter.DecomposeNode(_entityFactory.Serialize(source));
        var decomposedTarget = EntityWriter.DecomposeNode(_entityFactory.Serialize(target));
        var relationshipRecord = EntityWriter.DecomposeRelationship(_entityFactory.Serialize(relationship), relationship);

        var sourceConstraints = ConstraintChecker.From(_schemaRegistry.GetNodeSchema(decomposedSource.Node.Label));
        var targetConstraints = ConstraintChecker.From(_schemaRegistry.GetNodeSchema(decomposedTarget.Node.Label));
        var relationshipConstraints = ConstraintChecker.From(_schemaRegistry.GetRelationshipSchema(relationshipRecord.Type));

        // Both nodes and the edge are applied within one transaction unit: any failure (a duplicate
        // endpoint id under default semantics, a constraint violation, ...) rolls the whole thing
        // back, so nothing is created.
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    state = AddOrMergeEndpoint(
                        state,
                        decomposedSource,
                        sourceConstraints,
                        createMissingEndpoints);
                    state = AddOrMergeEndpoint(
                        state,
                        decomposedTarget,
                        targetConstraints,
                        createMissingEndpoints);

                    if (relationshipConstraints is not null)
                    {
                        ConstraintChecker.CheckRelationship(state, relationshipRecord, relationshipConstraints);
                    }

                    return state.AddRelationship(relationshipRecord);
                });
                return true;
            },
            $"Failed to create subgraph for relationship of type {typeof(TRelationship).Name}",
            cancellationToken).ConfigureAwait(false);

        RuntimeMetadata.PopulateNodeLabels(source, decomposedSource.Node.Labels);
        RuntimeMetadata.PopulateNodeLabels(target, decomposedTarget.Node.Labels);
        RuntimeMetadata.PopulateRelationshipType(relationship, relationshipRecord.Type);
    }

    private static StoreState AddOrMergeEndpoint(
        StoreState state,
        EntityWriter.DecomposedNode decomposed,
        ConstraintChecker.Constraints? constraints,
        bool createMissingEndpoints)
    {
        // MERGE-by-id semantics: if the endpoint already exists, reuse it as-is (no clobber,
        // no duplicate, no new complex-property subtree).
        if (createMissingEndpoints && state.RootNodes(decomposed.Node.Id).Count > 0)
        {
            return state;
        }

        if (constraints is not null)
        {
            ConstraintChecker.CheckNode(state, decomposed.Node, constraints);
        }

        return state.AddNode(decomposed.Node, decomposed.ComplexValueNodes, decomposed.ComplexEdges);
    }

    public async Task UpdateNodeAsync<N>(
        N node,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where N : class, INode
    {
        if (node is null)
        {
            throw new ArgumentException("Node cannot be null.", nameof(node));
        }

        if (string.IsNullOrEmpty(node.Id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(node));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        GraphDataModel.EnforceGraphConstraintsForNode(node);
        _validator.ValidateNode(node);

        var decomposed = EntityWriter.DecomposeNode(_entityFactory.Serialize(node));
        var constraints = ConstraintChecker.From(_schemaRegistry.GetNodeSchema(decomposed.Node.Label));
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckNode(state, decomposed.Node, constraints);
                    }

                    return state.UpdateNode(decomposed.Node, decomposed.ComplexValueNodes, decomposed.ComplexEdges);
                });
                return true;
            },
            $"Failed to update node {node.Id} of type {typeof(N).Name}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateRelationshipAsync<R>(
        R relationship,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where R : class, IRelationship
    {
        if (relationship is null)
        {
            throw new ArgumentException("Relationship cannot be null.", nameof(relationship));
        }

        if (string.IsNullOrEmpty(relationship.Id))
        {
            throw new ArgumentException("Relationship ID cannot be null or empty.", nameof(relationship));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        GraphDataModel.EnforceGraphConstraintsForRelationship(relationship);
        _validator.ValidateRelationship(relationship);

        var record = EntityWriter.DecomposeRelationship(_entityFactory.Serialize(relationship), relationship);
        var constraints = ConstraintChecker.From(_schemaRegistry.GetRelationshipSchema(record.Type));
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckRelationship(state, record, constraints);
                    }

                    return state.UpdateRelationship(record);
                });
                return true;
            },
            $"Failed to update relationship {relationship.Id} of type {typeof(R).Name}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteNodeAsync(
        string id,
        bool cascadeDelete = false,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state => state.DeleteNode(id, cascadeDelete));
                return true;
            },
            $"Failed to delete node {id}",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRelationshipAsync(
        string id,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state => state.DeleteRelationship(id));
                return true;
            },
            $"Failed to delete relationship {id}",
            cancellationToken).ConfigureAwait(false);
    }

    public IGraphQueryable<IEntity> Search(string query, IGraphTransaction? transaction = null) =>
        SearchQuery<IEntity>(query, SearchRootTarget.Entities, transaction);

    public IGraphQueryable<INode> SearchNodes(string query, IGraphTransaction? transaction = null) =>
        SearchQuery<INode>(query, SearchRootTarget.Nodes, transaction);

    public IGraphQueryable<IRelationship> SearchRelationships(string query, IGraphTransaction? transaction = null) =>
        SearchQuery<IRelationship>(query, SearchRootTarget.Relationships, transaction);

    public IGraphQueryable<T> SearchNodes<T>(string query, IGraphTransaction? transaction = null)
        where T : class, INode =>
        SearchQuery<T>(query, SearchRootTarget.Nodes, transaction);

    public IGraphQueryable<T> SearchRelationships<T>(string query, IGraphTransaction? transaction = null)
        where T : class, IRelationship =>
        SearchQuery<T>(query, SearchRootTarget.Relationships, transaction);

    public async Task RecreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The in-memory store has no indexes; ensuring the schema registry is initialized is the
        // whole of index provisioning here.
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private InMemoryQueryable<T> Query<T>(IGraphTransaction? transaction)
    {
        var (effective, owned) = ResolveQueryTransaction(transaction);
        var provider = new InMemoryQueryProvider(this, _store, owned ? null : effective, _reader);
        return new InMemoryQueryable<T>(provider);
    }

    private InMemoryQueryable<T> SearchQuery<T>(string query, SearchRootTarget target, IGraphTransaction? transaction)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (effective, owned) = ResolveQueryTransaction(transaction);
        var provider = new InMemoryQueryProvider(this, _store, owned ? null : effective, _reader);
        return new InMemoryQueryable<T>(provider, new InMemorySearchRootExpression(query, typeof(T), target));
    }

    private (InMemoryTransaction Transaction, bool Owned) ResolveQueryTransaction(IGraphTransaction? transaction) =>
        TransactionRunner.GetOrCreate(_store, transaction);

    private static NodeRecord? FindNodeRecord(StoreState state, string id, Type targetType)
    {
        return state.Nodes.Values.FirstOrDefault(record =>
            record.Id == id &&
            (targetType == typeof(DynamicNode) ||
             (!record.IsComplexValue &&
              targetType.IsAssignableFrom(EntityReader.ResolveNodeType(record, targetType)))));
    }

    private static RelationshipRecord? FindRelationshipRecord(StoreState state, string id, Type targetType)
    {
        return state.Relationships.TryGetValue(id, out var record) &&
            (targetType == typeof(DynamicRelationship) ||
             (!record.IsComplexProperty &&
              targetType.IsAssignableFrom(EntityReader.ResolveRelationshipType(record, targetType))))
            ? record
            : null;
    }
}

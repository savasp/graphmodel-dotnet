// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Cvoya.Graph.InMemory.Querying;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
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
                return (DynamicRelationship)_reader.MaterializeRelationship(
                    record,
                    typeof(DynamicRelationship),
                    includeLegacyEndpointState: true);
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
                return (R)_reader.MaterializeRelationship(
                    record,
                    typeof(R),
                    includeLegacyEndpointState: true);
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

        var entity = _entityFactory.Serialize(relationship);
        var constraints = ConstraintChecker.From(_schemaRegistry.GetRelationshipSchema(entity.Label));
        RelationshipRecord? record = null;
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    if (record is null)
                    {
                        var start = ResolveLegacyEndpoint(state, relationship.StartNodeId, "start");
                        var end = ResolveLegacyEndpoint(state, relationship.EndNodeId, "end");
                        record = EntityWriter.DecomposeRelationship(
                            entity,
                            start.Key,
                            end.Key,
                            LegacyRelationshipEndpoints.LegacyDirection(relationship));
                    }

                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckRelationship(state, record!, constraints);
                    }

                    return state.AddRelationship(record!);
                });
                return true;
            },
            $"Failed to create relationship of type {typeof(R).Name}",
            cancellationToken).ConfigureAwait(false);

        RuntimeMetadata.PopulateRelationshipType(relationship, record!.Type);
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
        var relationshipRecord = EntityWriter.DecomposeRelationship(
            _entityFactory.Serialize(relationship),
            decomposedSource.Node.Key,
            decomposedTarget.Node.Key,
            LegacyRelationshipEndpoints.LegacyDirection(relationship));

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
                    (state, var sourceKey) = AddOrMergeEndpoint(
                        state,
                        decomposedSource,
                        sourceConstraints,
                        createMissingEndpoints);
                    (state, var targetKey) = AddOrMergeEndpoint(
                        state,
                        decomposedTarget,
                        targetConstraints,
                        createMissingEndpoints);

                    if (relationshipConstraints is not null)
                    {
                        ConstraintChecker.CheckRelationship(
                            state,
                            relationshipRecord with { StartKey = sourceKey, EndKey = targetKey },
                            relationshipConstraints);
                    }

                    return state.AddRelationship(
                        relationshipRecord with { StartKey = sourceKey, EndKey = targetKey });
                });
                return true;
            },
            $"Failed to create subgraph for relationship of type {typeof(TRelationship).Name}",
            cancellationToken).ConfigureAwait(false);

        RuntimeMetadata.PopulateNodeLabels(source, decomposedSource.Node.Labels);
        RuntimeMetadata.PopulateNodeLabels(target, decomposedTarget.Node.Labels);
        RuntimeMetadata.PopulateRelationshipType(relationship, relationshipRecord.Type);
    }

    private static (StoreState State, Guid Key) AddOrMergeEndpoint(
        StoreState state,
        EntityWriter.DecomposedNode decomposed,
        ConstraintChecker.Constraints? constraints,
        bool createMissingEndpoints)
    {
        // MERGE-by-id semantics: if the endpoint already exists, reuse it as-is (no clobber,
        // no duplicate, no new complex-property subtree).
        var compatibilityId = decomposed.Node.CompatibilityId;
        var existing = compatibilityId is not null
            ? state.RootNodes(compatibilityId)
            : [];
        if (createMissingEndpoints && existing.Count > 0)
        {
            if (existing.Count > 1)
            {
                throw new GraphException(
                    $"Node ID {compatibilityId} matches {existing.Count} nodes; refusing an ambiguous merge.");
            }

            return (state, existing[0].Key);
        }

        if (!createMissingEndpoints && existing.Count > 0)
        {
            throw new GraphException(
                $"A node with ID {compatibilityId} already exists for this legacy create-only operation.");
        }

        if (constraints is not null)
        {
            ConstraintChecker.CheckNode(state, decomposed.Node, constraints);
        }

        return (
            state.AddNode(decomposed.Node, decomposed.ComplexValueNodes, decomposed.ComplexEdges),
            decomposed.Node.Key);
    }

    internal async Task CreateCommandRelationshipAsync(
        InMemoryTransaction transaction,
        GraphCommandEndpoint source,
        IRelationship relationship,
        GraphCommandEndpoint target,
        RelationshipDirection direction,
        GraphRelationshipCreationMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _schemaRegistry.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var newSource = PrepareNewEndpoint(source);
        var newTarget = mode == GraphRelationshipCreationMode.SelfLoop
            ? PrepareSelfLoopTarget(source, target, newSource)
            : PrepareNewEndpoint(target);
        var sourceKey = EndpointKey(source, newSource, GraphEndpointRole.Source);
        var targetKey = mode == GraphRelationshipCreationMode.SelfLoop
            ? sourceKey
            : EndpointKey(target, newTarget, GraphEndpointRole.Target);

        relationship.EnsureNoReferenceCycle();
        relationship.EnsureComplexPropertyDepth();
        _validator.ValidateRelationship(relationship);
        var relationshipRecord = EntityWriter.DecomposeRelationship(
            _entityFactory.Serialize(relationship),
            sourceKey,
            targetKey,
            mode == GraphRelationshipCreationMode.SelfLoop
                ? RelationshipDirection.Outgoing
                : direction);
        var relationshipConstraints = ConstraintChecker.From(
            _schemaRegistry.GetRelationshipSchema(relationshipRecord.Type));
        var sourceConstraints = newSource is null
            ? null
            : ConstraintChecker.From(_schemaRegistry.GetNodeSchema(newSource.Node.Label));
        var targetConstraints = newTarget is null || ReferenceEquals(newSource, newTarget)
            ? null
            : ConstraintChecker.From(_schemaRegistry.GetNodeSchema(newTarget.Node.Label));

        transaction.Apply(state =>
        {
            state = AddCommandEndpoint(
                state,
                source,
                newSource,
                sourceConstraints,
                GraphEndpointRole.Source);
            if (mode != GraphRelationshipCreationMode.SelfLoop)
            {
                state = AddCommandEndpoint(
                    state,
                    target,
                    newTarget,
                    targetConstraints,
                    GraphEndpointRole.Target);
            }

            if (relationshipConstraints is not null)
            {
                ConstraintChecker.CheckRelationship(state, relationshipRecord, relationshipConstraints);
            }

            return state.AddRelationship(relationshipRecord);
        });

        if (source is NewGraphCommandEndpoint sourceEndpoint)
        {
            RuntimeMetadata.PopulateNodeLabels(sourceEndpoint.Node, newSource!.Node.Labels);
        }

        if (mode != GraphRelationshipCreationMode.SelfLoop && target is NewGraphCommandEndpoint targetEndpoint)
        {
            RuntimeMetadata.PopulateNodeLabels(targetEndpoint.Node, newTarget!.Node.Labels);
        }

        RuntimeMetadata.PopulateRelationshipType(relationship, relationshipRecord.Type);
    }

    private EntityWriter.DecomposedNode? PrepareNewEndpoint(GraphCommandEndpoint endpoint)
    {
        if (endpoint is not NewGraphCommandEndpoint newEndpoint)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(newEndpoint.Node);
        newEndpoint.Node.EnsureNoReferenceCycle();
        newEndpoint.Node.EnsureComplexPropertyDepth();
        _validator.ValidateNode(newEndpoint.Node);
        return EntityWriter.DecomposeNode(_entityFactory.Serialize(newEndpoint.Node));
    }

    private static EntityWriter.DecomposedNode? PrepareSelfLoopTarget(
        GraphCommandEndpoint source,
        GraphCommandEndpoint target,
        EntityWriter.DecomposedNode? newSource)
    {
        if (source is not NewGraphCommandEndpoint sourceEndpoint ||
            target is not NewGraphCommandEndpoint targetEndpoint ||
            !ReferenceEquals(sourceEndpoint.Node, targetEndpoint.Node) ||
            newSource is null)
        {
            throw new GraphException(
                "Explicit self-loop creation requires the same new node as both endpoint operands.");
        }

        return newSource;
    }

    private static Guid EndpointKey(
        GraphCommandEndpoint endpoint,
        EntityWriter.DecomposedNode? newEndpoint,
        GraphEndpointRole role) => endpoint switch
        {
            SelectedGraphCommandEndpoint { Element.Kind: GraphElementKind.Node, Element.NativeIdentity: Guid key } => key,
            NewGraphCommandEndpoint when newEndpoint is not null => newEndpoint.Node.Key,
            SelectedGraphCommandEndpoint => throw new GraphException(
                $"The {role.ToString().ToLowerInvariant()} endpoint selection is not a private node binding."),
            _ => throw new GraphException(
                $"The {role.ToString().ToLowerInvariant()} endpoint operand is invalid."),
        };

    private static StoreState AddCommandEndpoint(
        StoreState state,
        GraphCommandEndpoint endpoint,
        EntityWriter.DecomposedNode? newEndpoint,
        ConstraintChecker.Constraints? constraints,
        GraphEndpointRole role)
    {
        if (endpoint is SelectedGraphCommandEndpoint selected)
        {
            var key = EndpointKey(selected, newEndpoint: null, role);
            if (!state.Nodes.TryGetValue(key, out var record) || record.IsComplexValue)
            {
                throw new GraphException(
                    $"The selected {role.ToString().ToLowerInvariant()} endpoint no longer exists in the transaction view.");
            }

            return state;
        }

        if (endpoint is not NewGraphCommandEndpoint || newEndpoint is null)
        {
            throw new GraphException(
                $"The {role.ToString().ToLowerInvariant()} endpoint operand is invalid.");
        }

        if (constraints is not null)
        {
            ConstraintChecker.CheckNode(state, newEndpoint.Node, constraints);
        }

        return state.AddNode(newEndpoint.Node, newEndpoint.ComplexValueNodes, newEndpoint.ComplexEdges);
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
                    var existing = ResolveLegacyNodeForUpdate(state, node.Id, decomposed.Node.Label);
                    var replacement = decomposed.WithRootKey(existing.Key);
                    if (constraints is not null)
                    {
                        ConstraintChecker.CheckNode(state, replacement.Node, constraints);
                    }

                    return state.UpdateNode(
                        replacement.Node,
                        replacement.ComplexValueNodes,
                        replacement.ComplexEdges);
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

        var entity = _entityFactory.Serialize(relationship);
        var constraints = ConstraintChecker.From(_schemaRegistry.GetRelationshipSchema(entity.Label));
        await TransactionRunner.ExecuteAsync(
            _store,
            transaction,
            tx =>
            {
                tx.Apply(state =>
                {
                    var existing = ResolveLegacyRelationshipForUpdate(state, relationship.Id);

                    // Endpoints are immutable on update, so the caller-supplied IDs are validated
                    // against the stored endpoints instead of re-resolved globally: duplicate
                    // public IDs elsewhere in the graph must not fail an unambiguous property
                    // update on a keyed record.
                    EnsureLegacyEndpointUnchanged(state, existing.StartKey, relationship.StartNodeId);
                    EnsureLegacyEndpointUnchanged(state, existing.EndKey, relationship.EndNodeId);
                    var record = EntityWriter.DecomposeRelationship(
                        entity,
                        existing.StartKey,
                        existing.EndKey,
                        LegacyRelationshipEndpoints.LegacyDirection(relationship),
                        existing.Key);
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

    public Task RecreateManagedIndexesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
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
            record.CompatibilityId == id &&
            (targetType == typeof(DynamicNode) ||
             (!record.IsComplexValue &&
              targetType.IsAssignableFrom(EntityReader.ResolveNodeType(record, targetType)))));
    }

    private static RelationshipRecord? FindRelationshipRecord(StoreState state, string id, Type targetType)
    {
        return state.Relationships.Values.FirstOrDefault(record =>
            record.CompatibilityId == id &&
            !record.IsComplexProperty &&
            (targetType == typeof(DynamicRelationship) ||
             targetType.IsAssignableFrom(EntityReader.ResolveRelationshipType(record, targetType))));
    }

    private static NodeRecord ResolveLegacyEndpoint(StoreState state, string id, string role)
    {
        var matches = state.RootNodes(id);
        return matches.Count switch
        {
            0 => throw new GraphException(
                $"Cannot create relationship: {role} node {id} does not exist."),
            1 => matches[0],
            _ => throw new GraphException(
                $"Cannot create relationship: {role} node ID {id} matches {matches.Count} nodes."),
        };
    }

    private static NodeRecord ResolveLegacyNodeForUpdate(StoreState state, string id, string label)
    {
        var matches = state.RootNodes(id)
            .Where(node => string.Equals(node.Label, label, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            0 => throw new EntityNotFoundException($"Node with ID {id} not found for update"),
            1 => matches[0],
            _ => throw new GraphException(
                $"Node ID {id} and label {label} match {matches.Length} nodes; refusing an ambiguous update."),
        };
    }

    private static void EnsureLegacyEndpointUnchanged(StoreState state, Guid storedKey, string requestedId)
    {
        if (!string.Equals(state.Nodes[storedKey].CompatibilityId, requestedId, StringComparison.Ordinal))
        {
            throw new GraphException(
                "Relationship endpoints cannot be changed on update; delete and recreate the relationship.");
        }
    }

    private static RelationshipRecord ResolveLegacyRelationshipForUpdate(
        StoreState state,
        string id)
    {
        var matches = state.Relationships.Values
            .Where(relationship =>
                !relationship.IsComplexProperty &&
                relationship.CompatibilityId == id)
            .ToArray();
        return matches.Length switch
        {
            0 => throw new EntityNotFoundException($"Relationship with ID {id} not found for update"),
            1 => matches[0],
            _ => throw new GraphException(
                $"Relationship ID {id} matches {matches.Length} relationships; refusing an ambiguous update."),
        };
    }
}

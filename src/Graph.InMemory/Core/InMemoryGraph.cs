// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

using Cvoya.Graph.InMemory.Querying;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;

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

    public InMemoryGraph(InMemoryStore store, SchemaRegistry schemaRegistry, ILoggerFactory? loggerFactory)
    {
        _store = store;
        _schemaRegistry = schemaRegistry;
        _entityFactory = new EntityFactory(loggerFactory);
        _reader = new EntityReader(_entityFactory);
        _validator = new EntityValidator(schemaRegistry);
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
    }

    public Task CreateAsync<TSource, TRelationship, TTarget>(
        TSource source,
        TRelationship relationship,
        TTarget target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
        => GraphCommandExtensions.CreateNewAsync(
            this,
            source,
            relationship,
            target,
            direction,
            GraphRelationshipCreationMode.Standard,
            transaction,
            cancellationToken);

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

}

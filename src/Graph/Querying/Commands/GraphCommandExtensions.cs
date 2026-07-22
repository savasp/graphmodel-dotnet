// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>Provides set-based graph mutations and endpoint-intent relationship creation.</summary>
public static class GraphCommandExtensions
{
    private static readonly MethodInfo UpdateDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.UpdateMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo DeleteDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.DeleteMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Updates every distinct graph entity selected by <paramref name="source"/>.</summary>
    /// <typeparam name="TEntity">The selected graph entity type.</typeparam>
    /// <param name="source">The query selecting the frozen mutation target set.</param>
    /// <param name="setters">The mapped property assignments to apply.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of distinct affected graph entities.</returns>
    public static Task<int> UpdateAsync<TEntity>(
        this IGraphQueryable<TEntity> source,
        Expression<Func<GraphPropertySetters<TEntity>, GraphPropertySetters<TEntity>>> setters,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(setters);
        return source.Provider.ExecuteAsync<int>(
            Expression.Call(
                UpdateDefinition.MakeGenericMethod(typeof(TEntity)),
                source.Expression,
                Expression.Quote(setters)),
            cancellationToken);
    }

    /// <summary>Deletes every distinct node selected by <paramref name="source"/>.</summary>
    /// <param name="source">The query selecting the frozen deletion target set.</param>
    /// <param name="cascadeDelete">Whether user-defined relationships may be deleted with selected nodes.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of distinct affected nodes.</returns>
    public static Task<int> DeleteAsync(
        this IGraphQueryable<INode> source,
        bool cascadeDelete = false,
        CancellationToken cancellationToken = default)
        => DeleteCoreAsync(source, cascadeDelete, cancellationToken);

    /// <summary>Deletes every distinct relationship selected by <paramref name="source"/>.</summary>
    /// <param name="source">The query selecting the frozen deletion target set.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of distinct affected relationships.</returns>
    public static Task<int> DeleteAsync(
        this IGraphQueryable<IRelationship> source,
        CancellationToken cancellationToken = default)
        => DeleteCoreAsync(source, cascadeDelete: false, cancellationToken);

    /// <summary>Creates a relationship between two existing, exactly-one node selections.</summary>
    /// <typeparam name="TSource">The selected source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <typeparam name="TTarget">The selected target node type.</typeparam>
    /// <param name="graph">The graph that owns both selections.</param>
    /// <param name="source">The exactly-one source selection.</param>
    /// <param name="relationship">The new relationship.</param>
    /// <param name="target">The exactly-one target selection.</param>
    /// <param name="direction">The direction in which to create the relationship.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static async Task CreateRelationshipAsync<TSource, TRelationship, TTarget>(
        this IGraph graph,
        IGraphQueryable<TSource> source,
        TRelationship relationship,
        IGraphQueryable<TTarget> target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var graphProvider = GetGraphProvider(graph);
        var sourceProvider = GetCommandProvider(source);
        var targetProvider = GetCommandProvider(target);
        GraphCommandProviderScope.ValidateGraph(graphProvider, sourceProvider);
        GraphCommandProviderScope.ValidateGraph(graphProvider, targetProvider);
        GraphCommandProviderScope.Validate(sourceProvider, targetProvider);
        var sourceSelection = BuildSelection(source);
        var targetSelection = BuildSelection(target);

        await sourceProvider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await sourceProvider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var selectedSource = await SelectAsync(
                    context, source, sourceSelection, GraphEndpointRole.Source, token).ConfigureAwait(false);
                var selectedTarget = await SelectAsync(
                    context, target, targetSelection, GraphEndpointRole.Target, token).ConfigureAwait(false);
                await context.CreateRelationshipAsync(
                    new SelectedGraphCommandEndpoint(selectedSource),
                    relationship,
                    new SelectedGraphCommandEndpoint(selectedTarget),
                    direction,
                    GraphRelationshipCreationMode.Standard,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a relationship from one existing source node to a new target node.</summary>
    /// <typeparam name="TSource">The selected source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <typeparam name="TTarget">The new target node type.</typeparam>
    /// <param name="graph">The graph that owns the source selection.</param>
    /// <param name="source">The exactly-one source selection.</param>
    /// <param name="relationship">The new relationship.</param>
    /// <param name="newTarget">The new target node.</param>
    /// <param name="direction">The direction in which to create the relationship.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static Task CreateAsync<TSource, TRelationship, TTarget>(
        this IGraph graph,
        IGraphQueryable<TSource> source,
        TRelationship relationship,
        TTarget newTarget,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode =>
        CreateHybridAsync(
            graph,
            source,
            relationship,
            new NewGraphCommandEndpoint(newTarget ?? throw new ArgumentNullException(nameof(newTarget))),
            GraphEndpointRole.Source,
            direction,
            cancellationToken);

    /// <summary>Creates a relationship from a new source node to one existing target node.</summary>
    /// <typeparam name="TSource">The new source node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <typeparam name="TTarget">The selected target node type.</typeparam>
    /// <param name="graph">The graph that owns the target selection.</param>
    /// <param name="newSource">The new source node.</param>
    /// <param name="relationship">The new relationship.</param>
    /// <param name="target">The exactly-one target selection.</param>
    /// <param name="direction">The direction in which to create the relationship.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static Task CreateAsync<TSource, TRelationship, TTarget>(
        this IGraph graph,
        TSource newSource,
        TRelationship relationship,
        IGraphQueryable<TTarget> target,
        RelationshipDirection direction = RelationshipDirection.Outgoing,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode =>
        CreateHybridAsync(
            graph,
            target,
            relationship,
            new NewGraphCommandEndpoint(newSource ?? throw new ArgumentNullException(nameof(newSource))),
            GraphEndpointRole.Target,
            direction,
            cancellationToken);

    /// <summary>Creates one new node and a relationship from that node to itself.</summary>
    /// <typeparam name="TNode">The new node type.</typeparam>
    /// <typeparam name="TRelationship">The relationship type.</typeparam>
    /// <param name="graph">The graph in which to create the self-loop.</param>
    /// <param name="node">The new node.</param>
    /// <param name="relationship">The new self-loop relationship.</param>
    /// <param name="transaction">The transaction to use, or <see langword="null"/> to use an owned transaction.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static Task CreateSelfLoopAsync<TNode, TRelationship>(
        this IGraph graph,
        TNode node,
        TRelationship relationship,
        IGraphTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        where TNode : class, INode
        where TRelationship : class, IRelationship =>
        CreateNewAsync(
            graph,
            node,
            relationship,
            node,
            RelationshipDirection.Outgoing,
            GraphRelationshipCreationMode.SelfLoop,
            transaction,
            cancellationToken);

    internal static async Task CreateNewAsync<TSource, TRelationship, TTarget>(
        IGraph graph,
        TSource source,
        TRelationship relationship,
        TTarget target,
        RelationshipDirection direction,
        GraphRelationshipCreationMode mode,
        IGraphTransaction? transaction,
        CancellationToken cancellationToken)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetGraphProvider(graph, transaction);
        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                await context.CreateRelationshipAsync(
                    new NewGraphCommandEndpoint(source),
                    relationship,
                    new NewGraphCommandEndpoint(target),
                    direction,
                    mode,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> DeleteCoreAsync<TEntity>(
        IGraphQueryable<TEntity> source,
        bool cascadeDelete,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Provider.ExecuteAsync<int>(
            Expression.Call(
                DeleteDefinition.MakeGenericMethod(typeof(TEntity)),
                source.Expression,
                Expression.Constant(cascadeDelete)),
            cancellationToken);
    }

    private static async Task CreateHybridAsync<TEntity>(
        IGraph graph,
        IGraphQueryable<TEntity> selected,
        IRelationship relationship,
        NewGraphCommandEndpoint newEndpoint,
        GraphEndpointRole selectedRole,
        RelationshipDirection direction,
        CancellationToken cancellationToken)
        where TEntity : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(relationship);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var graphProvider = GetGraphProvider(graph);
        var provider = GetCommandProvider(selected);
        GraphCommandProviderScope.ValidateGraph(graphProvider, provider);
        var selection = BuildSelection(selected);

        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await provider.InWriteTransactionAsync(
            async (context, token) =>
            {
                var endpoint = new SelectedGraphCommandEndpoint(await SelectAsync(
                    context, selected, selection, selectedRole, token).ConfigureAwait(false));
                GraphCommandEndpoint source = selectedRole == GraphEndpointRole.Source ? endpoint : newEndpoint;
                GraphCommandEndpoint target = selectedRole == GraphEndpointRole.Target ? endpoint : newEndpoint;
                await context.CreateRelationshipAsync(
                    source,
                    relationship,
                    target,
                    direction,
                    GraphRelationshipCreationMode.Standard,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static IGraphCommandProvider GetGraphProvider(
        IGraph graph,
        IGraphTransaction? transaction = null) =>
        GetCommandProvider(graph.Nodes<INode>(transaction));

    private static IGraphCommandProvider GetCommandProvider<TEntity>(IGraphQueryable<TEntity> query) =>
        query.Provider as IGraphCommandProvider ?? throw new NotSupportedException(
            "The graph query provider does not support graph commands.");

    private static GraphElementSelectionModel BuildSelection<TEntity>(IGraphQueryable<TEntity> query)
    {
        var selection = new GraphElementSelectionModel(
            GraphQueryModelBuilder.Build(query.Expression),
            GraphElementSelectionMode.ExactOne);
        GraphElementSelectionModelValidator.Validate(selection);
        if (selection.ElementKind != GraphElementKind.Node)
        {
            throw new GraphQueryTranslationException("A relationship endpoint command must select nodes.");
        }

        return selection;
    }

    private static Task<SelectedGraphElement> SelectAsync<TEntity>(
        IGraphCommandExecutionContext context,
        IGraphQueryable<TEntity> query,
        GraphElementSelectionModel selection,
        GraphEndpointRole role,
        CancellationToken cancellationToken) =>
        GraphCommandSelection.SelectExactOneAsync(
            context,
            selection,
            query.Expression,
            role,
            cancellationToken);

    private static void ValidateDirection(RelationshipDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
}

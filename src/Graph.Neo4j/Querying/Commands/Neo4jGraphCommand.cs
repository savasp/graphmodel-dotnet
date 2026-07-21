// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Querying.Commands;

using System.Linq.Expressions;
using Cvoya.Graph.Neo4j.Querying.Linq.Providers;
using Cvoya.Graph.Querying;
using Cvoya.Graph.Querying.Commands;

/// <summary>
/// Internal Neo4j command surface used to exercise endpoint-intent creation before the coordinated
/// public API switch. Every selected endpoint is resolved and consumed inside one write transaction.
/// </summary>
internal static class Neo4jGraphCommand
{
    public static Task<int> UpdateAsync<TEntity>(
        IGraphQueryable<TEntity> source,
        Expression<Func<GraphPropertySetters<TEntity>, GraphPropertySetters<TEntity>>> setters,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity =>
        GraphCommandExtensions.UpdateAsync(source, setters, cancellationToken);

    public static Task<int> DeleteAsync<TEntity>(
        IGraphQueryable<TEntity> source,
        bool cascadeDelete,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity =>
        GraphCommandExtensions.DeleteAsync(source, cascadeDelete, cancellationToken);

    public static async Task CreateRelationshipAsync<TSource, TRelationship, TTarget>(
        IGraph graph,
        IGraphQueryable<TSource> source,
        TRelationship relationship,
        IGraphQueryable<TTarget> target,
        RelationshipDirection direction,
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

        var sourceProvider = GetProvider(graph, source);
        var targetProvider = GetProvider(graph, target);
        var sourceCommandProvider = (IGraphCommandProvider)sourceProvider;
        var targetCommandProvider = (IGraphCommandProvider)targetProvider;
        GraphCommandProviderScope.Validate(sourceCommandProvider, targetCommandProvider);
        var sourceSelection = BuildSelection(source);
        var targetSelection = BuildSelection(target);

        await sourceProvider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await sourceCommandProvider.InWriteTransactionAsync(
            async (command, token) =>
            {
                var context = RequireNeo4jContext(command);
                var selectedSource = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    sourceSelection,
                    source.Expression,
                    GraphEndpointRole.Source,
                    token).ConfigureAwait(false);
                var selectedTarget = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    targetSelection,
                    target.Expression,
                    GraphEndpointRole.Target,
                    token).ConfigureAwait(false);
                await context.CreateRelationshipAsync(
                    selectedSource,
                    relationship,
                    selectedTarget,
                    direction,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateAsync<TSource, TRelationship, TTarget>(
        IGraph graph,
        IGraphQueryable<TSource> source,
        TRelationship relationship,
        TTarget newTarget,
        RelationshipDirection direction,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(newTarget);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetProvider(graph, source);
        var commandProvider = (IGraphCommandProvider)provider;
        var selection = BuildSelection(source);

        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await commandProvider.InWriteTransactionAsync(
            async (command, token) =>
            {
                var context = RequireNeo4jContext(command);
                var selectedSource = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    selection,
                    source.Expression,
                    GraphEndpointRole.Source,
                    token).ConfigureAwait(false);
                await context.CreateAsync(
                    selectedSource,
                    relationship,
                    newTarget,
                    direction,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateAsync<TSource, TRelationship, TTarget>(
        IGraph graph,
        TSource newSource,
        TRelationship relationship,
        IGraphQueryable<TTarget> target,
        RelationshipDirection direction,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(newSource);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(target);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetProvider(graph, target);
        var commandProvider = (IGraphCommandProvider)provider;
        var selection = BuildSelection(target);

        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await commandProvider.InWriteTransactionAsync(
            async (command, token) =>
            {
                var context = RequireNeo4jContext(command);
                var selectedTarget = await GraphCommandSelection.SelectExactOneAsync(
                    context,
                    selection,
                    target.Expression,
                    GraphEndpointRole.Target,
                    token).ConfigureAwait(false);
                await context.CreateAsync(
                    newSource,
                    relationship,
                    selectedTarget,
                    direction,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateAsync<TSource, TRelationship, TTarget>(
        IGraph graph,
        TSource newSource,
        TRelationship relationship,
        TTarget newTarget,
        RelationshipDirection direction,
        CancellationToken cancellationToken = default)
        where TSource : class, INode
        where TRelationship : class, IRelationship
        where TTarget : class, INode
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(newSource);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(newTarget);
        ValidateDirection(direction);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetProvider(graph, graph.Nodes<INode>());
        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await ((IGraphCommandProvider)provider).InWriteTransactionAsync(
            async (command, token) =>
            {
                await RequireNeo4jContext(command).CreateAsync(
                    newSource,
                    relationship,
                    newTarget,
                    direction,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task CreateSelfLoopAsync<TNode, TRelationship>(
        IGraph graph,
        TNode node,
        TRelationship relationship,
        CancellationToken cancellationToken = default)
        where TNode : class, INode
        where TRelationship : class, IRelationship
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(relationship);
        cancellationToken.ThrowIfCancellationRequested();

        var provider = GetProvider(graph, graph.Nodes<INode>());
        await provider.PrepareRelationshipCreationAsync(cancellationToken).ConfigureAwait(false);
        _ = await ((IGraphCommandProvider)provider).InWriteTransactionAsync(
            async (command, token) =>
            {
                await RequireNeo4jContext(command).CreateSelfLoopAsync(
                    node,
                    relationship,
                    token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static GraphQueryProvider GetProvider<TEntity>(
        IGraph graph,
        IGraphQueryable<TEntity> query)
    {
        if (query.Provider is not GraphQueryProvider provider || !ReferenceEquals(provider.Graph, graph))
        {
            throw new GraphException(
                "A selected relationship endpoint must belong to the receiver Neo4j graph instance.");
        }

        return provider;
    }

    private static GraphElementSelectionModel BuildSelection<TEntity>(IGraphQueryable<TEntity> query)
    {
        var selection = new GraphElementSelectionModel(
            GraphQueryModelBuilder.Build(query.Expression),
            GraphElementSelectionMode.ExactOne);
        GraphElementSelectionModelValidator.Validate(selection);
        if (selection.ElementKind != GraphElementKind.Node)
        {
            throw new GraphQueryTranslationException(
                "A relationship endpoint command must select nodes.");
        }

        return selection;
    }

    private static Neo4jGraphCommandExecutionContext RequireNeo4jContext(
        IGraphCommandExecutionContext context) =>
        context as Neo4jGraphCommandExecutionContext ?? throw new GraphException(
            "The selected relationship endpoint is not backed by the Neo4j command provider.");

    private static void ValidateDirection(RelationshipDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
}

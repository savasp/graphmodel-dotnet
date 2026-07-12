// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System.Linq.Expressions;
using System.Reflection;

using static Cvoya.Graph.ExtensionUtils;

/// <summary>
/// Extension methods for graph traversal operations built on top of PathSegments.
/// These methods provide convenient ways to traverse relationships and get target nodes,
/// relationships, or (for variable-length traversals) full paths.
/// </summary>
/// <remarks>
/// <para>
/// Graph operators are gated by generic constraints (<c>where TRel : IRelationship</c>, etc.) on a
/// single <see cref="IGraphQueryable{T}"/> rather than by a separate receiver interface
/// hierarchy — any <see cref="IGraphQueryable{T}"/> whose element type is an <see cref="INode"/>
/// can be traversed, regardless of what operators produced it (<c>Where</c>, <c>OrderBy</c>,
/// <c>Take</c>, ... all preserve the graph-typed chain by construction).
/// </para>
/// <para>
/// <b>Type-argument principle (issue #94, "Option C"):</b> <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode})"/>,
/// <see cref="TraverseRelationships{TRel, TEnd}(IGraphQueryable{INode})"/>, and
/// <see cref="TraversePaths{TRel, TEnd}(IGraphQueryable{INode}, int, int)"/> take only <c>TRel</c>
/// and <c>TEnd</c> as explicit type arguments: their result types (<c>TEnd</c>, <c>TRel</c>,
/// <see cref="IGraphPath"/>) never mention the start node type, so it is not spelled as a type
/// argument either. The start type instead rides in on the receiver: <see cref="IGraphQueryable{T}"/>
/// is covariant (<c>IGraphQueryable&lt;out T&gt;</c>), so any <c>IGraphQueryable&lt;TStart&gt;</c>
/// where <c>TStart : INode</c> converts to <c>IGraphQueryable&lt;INode&gt;</c> at the call site
/// (<c>people.Traverse&lt;Knows, Person&gt;()</c>), and the actual start type is recovered from the
/// source expression chain's element type at translation time (see the Neo4j provider's
/// <c>CypherQueryVisitor</c>), not from a generic argument a caller could mismatch under variance
/// widening. <see cref="PathSegments{TStartNode, TRelationship, TEndNode}"/> is the one exception:
/// its result type is <c>IGraphPathSegment&lt;TStart,TRel,TEnd&gt;</c>, which does name the start
/// type, so <c>TStart</c> remains a required, explicit type argument there. The principle is: you
/// spell exactly the types that appear in the result.
/// </para>
/// </remarks>
#pragma warning disable CS0618 // These operators are the sanctioned internal users of the now-obsolete free-floating WithDepth/Direction modifiers.
public static class GraphTraversalExtensions
{
    /// <summary>
    /// The foundational path segments method that all other single-hop traversal operations are
    /// built upon. Gets path segments representing the traversal from source nodes through
    /// relationships to target nodes.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <returns>A queryable of path segments representing the traversal</returns>
    /// <remarks>
    /// Unlike <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode})"/> and its siblings, this
    /// keeps all three type arguments explicit: the result type
    /// <see cref="IGraphPathSegment{TStartNode, TRelationship, TEndNode}"/> names
    /// <typeparamref name="TStartNode"/>, so it must be spelled (see the type-argument principle
    /// documented on <see cref="GraphTraversalExtensions"/>).
    /// </remarks>
    public static IGraphQueryable<IGraphPathSegment<TStartNode, TRelationship, TEndNode>> PathSegments<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphTraversalExtensions),
            nameof(PathSegments),
            3, // TStartNode, TRelationship, TEndNode
            1  // source
        ).MakeGenericMethod(typeof(TStartNode), typeof(TRelationship), typeof(TEndNode));

        var callExpression = Expression.Call(null, methodInfo, source.Expression);

        return source.Provider.CreateQuery<IGraphPathSegment<TStartNode, TRelationship, TEndNode>>(callExpression);
    }

    /// <summary>
    /// Builds the <see cref="PathSegments{TStartNode, TRelationship, TEndNode}"/> call expression
    /// for a source whose element type is only known at runtime (the two-arg
    /// <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode})"/> family): reflectively closes the
    /// generic <see cref="PathSegments{TStartNode, TRelationship, TEndNode}"/> method definition
    /// over the source's actual start type (recovered from its expression tree's static element
    /// type, not a caller-supplied generic argument) plus the statically-known
    /// <typeparamref name="TRel"/>/<typeparamref name="TEnd"/>.
    /// </summary>
    /// <remarks>
    /// Returns the plain <see cref="MethodCallExpression"/> call node (typed
    /// <c>IGraphQueryable&lt;IGraphPathSegment&lt;startType,TRel,TEnd&gt;&gt;</c>) rather than a
    /// materialized <c>IGraphQueryable&lt;T&gt;</c>: everything downstream (<c>WithDepth</c>,
    /// <c>Direction</c>, the final <c>Select</c>) is likewise built as a raw expression node via
    /// <see cref="ReflectiveTraversalChain"/> and only materialized into a real queryable once, at
    /// the very end - materializing an intermediate <c>IGraphQueryable&lt;object&gt;</c> would lose
    /// the real path-segment type from the expression tree (generic operators like
    /// <c>Direction&lt;TSource&gt;</c> would then close over <c>object</c> instead of the real
    /// path-segment type when called through that C#-typed intermediate).
    /// </remarks>
    private static MethodCallExpression BuildPathSegmentsCall<TRel, TEnd>(IGraphQueryable<INode> source, out Type startType)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        startType = ExtensionUtils.GetQueryableElementType(source.Expression.Type);

        var openMethod = GetGenericExtensionMethod(
            typeof(GraphTraversalExtensions),
            nameof(PathSegments),
            3, // TStartNode, TRelationship, TEndNode
            1  // source
        );

        var closedMethod = openMethod.MakeGenericMethod(startType, typeof(TRel), typeof(TEnd));

        return Expression.Call(null, closedMethod, source.Expression);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes. Any <see cref="IGraphQueryable{T}"/>
    /// whose element type is an <see cref="INode"/> converts to this parameter by covariance.</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEnd> Traverse<TRel, TEnd>(
        this IGraphQueryable<INode> source)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source);
        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with depth constraints.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEnd> Traverse<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        int maxDepth)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source).WithDepth(maxDepth);
        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with depth range constraints.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="minDepth">The minimum depth to traverse</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEnd> Traverse<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        int minDepth,
        int maxDepth)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source).WithDepth(minDepth, maxDepth);
        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with direction constraints.
    /// This overload allows specifying direction directly in the method call for better readability.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="direction">The direction to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEnd> Traverse<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        GraphTraversalDirection direction)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source).WithDirection(direction);
        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes, configured via an
    /// options lambda (depth range and/or direction).
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="configure">A callback that configures depth and/or direction.</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEnd> Traverse<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        Func<GraphTraversalOptions, GraphTraversalOptions> configure)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(configure);

        var options = configure(new GraphTraversalOptions());
        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source);

        if (options.TraversalDirection is { } direction)
            chain = chain.WithDirection(direction);

        chain = (options.MinDepth, options.MaxDepth) switch
        {
            (int min, int max) => chain.WithDepth(min, max),
            (null, int max) => chain.WithDepth(max),
            _ => chain
        };

        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships in reverse direction to get the source nodes.
    /// This is a convenience method for reverse traversal that makes the intent clear.
    /// Equivalent to Traverse with GraphTraversalDirection.Incoming.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes (where we end up)</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <returns>A queryable of target nodes reached through reverse traversal</returns>
    public static IGraphQueryable<TEnd> ReverseTraverse<TRel, TEnd>(
        this IGraphQueryable<INode> source)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source).WithDirection(GraphTraversalDirection.Incoming);
        return chain.SelectEndNode<TEnd>(source);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the relationships.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <returns>A queryable of relationships traversed</returns>
    public static IGraphQueryable<TRel> TraverseRelationships<TRel, TEnd>(
        this IGraphQueryable<INode> source)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        var chain = ReflectiveTraversalChain.FromPathSegments<TRel, TEnd>(source);
        return chain.SelectRelationship<TRel>(source);
    }

    /// <summary>
    /// Traverses a variable-length path of relationships of the specified type and returns the
    /// resulting <see cref="IGraphPath"/> instances (start node, end node, and the ordered
    /// single-hop segments in between). Use this instead of <see cref="PathSegments{TStartNode, TRelationship, TEndNode}"/>
    /// when the depth range spans more than a single hop, since a single
    /// <see cref="IGraphPathSegment{S,R,T}"/> cannot represent more than one hop.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="minDepth">The minimum depth to traverse (must be at least 1).</param>
    /// <param name="maxDepth">The maximum depth to traverse (must be greater than or equal to <paramref name="minDepth"/>).</param>
    /// <returns>A queryable of graph paths representing the variable-length traversal.</returns>
    public static IGraphQueryable<IGraphPath> TraversePaths<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        int minDepth,
        int maxDepth)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);

        if (minDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be at least 1.");

        if (maxDepth < minDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth.");

        // TStart is not needed here: this method only builds the TraversePaths<TRel, TEnd>
        // MethodCallExpression (a two-arg call - see the type-argument principle documented on
        // GraphTraversalExtensions); the actual start type is recovered later, by the provider's
        // visitor, from the resulting expression's source argument (see CypherQueryVisitor.
        // HandleTraversePaths / TypeHelpers.GetElementType(node.Arguments[0].Type)), not by this
        // client-side builder.
        var methodInfo = GetGenericExtensionMethod(
            typeof(GraphTraversalExtensions),
            nameof(TraversePaths),
            2, // TRel, TEnd
            3  // source, minDepth, maxDepth
        ).MakeGenericMethod(typeof(TRel), typeof(TEnd));

        var expression = Expression.Call(
            null,
            methodInfo,
            source.Expression,
            Expression.Constant(minDepth),
            Expression.Constant(maxDepth));

        return source.Provider.CreateQuery<IGraphPath>(expression);
    }

    /// <summary>
    /// Traverses a variable-length path of relationships of the specified type, configured via
    /// an options lambda (depth range and/or direction), and returns the resulting
    /// <see cref="IGraphPath"/> instances.
    /// </summary>
    /// <typeparam name="TRel">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEnd">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="configure">A callback that configures depth and/or direction. A depth range must be specified.</param>
    /// <returns>A queryable of graph paths representing the variable-length traversal.</returns>
    public static IGraphQueryable<IGraphPath> TraversePaths<TRel, TEnd>(
        this IGraphQueryable<INode> source,
        Func<GraphTraversalOptions, GraphTraversalOptions> configure)
        where TRel : class, IRelationship
        where TEnd : class, INode
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(configure);

        var options = configure(new GraphTraversalOptions());

        if (options.MaxDepth is not { } maxDepth)
            throw new ArgumentException("TraversePaths requires a depth range (call Depth(...) on the options).", nameof(configure));

        var minDepth = options.MinDepth ?? 1;

        var paths = source.TraversePaths<TRel, TEnd>(minDepth, maxDepth);

        return options.TraversalDirection is { } direction
            ? paths.Direction(direction)
            : paths;
    }

    /// <summary>
    /// Builds the "PathSegments, then optionally WithDepth/Direction, then Select" expression
    /// chain for the two-arg <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode})"/> family, as
    /// a single raw <see cref="Expression"/> tree - never materializing an intermediate
    /// <c>IGraphQueryable&lt;T&gt;</c> along the way.
    /// </summary>
    /// <remarks>
    /// <c>TStart</c> is not a compile-time generic argument on the two-arg operators (it is
    /// recovered at runtime from the source expression chain's element type - see
    /// <see cref="BuildPathSegmentsCall{TRel, TEnd}"/>), so every step of the chain
    /// (<c>WithDepth</c>, <c>Direction</c>, the final <c>Select</c>) must close its own generic
    /// method definition reflectively over that runtime-resolved <c>startType</c> instead of
    /// going through the statically-typed <see cref="GraphQueryableExtensions"/> extension
    /// methods. Materializing an intermediate <c>IGraphQueryable&lt;object&gt;</c> partway through
    /// (an earlier approach) does not work: those extension methods are themselves generic
    /// (<c>WithDepth&lt;TSource&gt;</c>, <c>Direction&lt;TSource&gt;</c>), so calling them through
    /// an <c>object</c>-typed C# receiver would close them over <c>object</c> instead of the real
    /// path-segment type, corrupting the expression tree the provider needs to translate.
    /// </remarks>
    private readonly struct ReflectiveTraversalChain
    {
        private readonly Expression _expression;
        private readonly Type _startType;
        private readonly Type _relType;

        private ReflectiveTraversalChain(Expression expression, Type startType, Type relType)
        {
            _expression = expression;
            _startType = startType;
            _relType = relType;
        }

        public static ReflectiveTraversalChain FromPathSegments<TRel, TEnd>(IGraphQueryable<INode> source)
            where TRel : class, IRelationship
            where TEnd : class, INode
        {
            var call = BuildPathSegmentsCall<TRel, TEnd>(source, out var startType);
            return new ReflectiveTraversalChain(call, startType, typeof(TRel));
        }

        public ReflectiveTraversalChain WithDepth(int maxDepth) => AppendCall(nameof(GraphQueryableExtensions.WithDepth), 2, Expression.Constant(maxDepth));

        public ReflectiveTraversalChain WithDepth(int minDepth, int maxDepth) => AppendCall(nameof(GraphQueryableExtensions.WithDepth), 3, Expression.Constant(minDepth), Expression.Constant(maxDepth));

        public ReflectiveTraversalChain WithDirection(GraphTraversalDirection direction) => AppendCall(nameof(GraphQueryableExtensions.Direction), 2, Expression.Constant(direction));

        private ReflectiveTraversalChain AppendCall(string methodName, int paramCount, params Expression[] extraArgs)
        {
            // The generic argument of WithDepth/Direction is the *segment* type - resolved from
            // the current expression's own static type (always
            // IGraphQueryable<IGraphPathSegment<start,rel,end>> at this point in the chain, since
            // only PathSegments/WithDepth/Direction precede the final Select), rather than from
            // TEnd, which this type doesn't carry.
            var currentSegmentType = ExtensionUtils.GetQueryableElementType(_expression.Type);

            var methodInfo = GetGenericExtensionMethod(
                typeof(GraphQueryableExtensions),
                methodName,
                1, // TSource
                paramCount
            ).MakeGenericMethod(currentSegmentType);

            var expression = Expression.Call(null, methodInfo, [_expression, .. extraArgs]);
            return new ReflectiveTraversalChain(expression, _startType, _relType);
        }

        public IGraphQueryable<TEnd> SelectEndNode<TEnd>(IGraphQueryable<INode> source)
            where TEnd : class, INode
            => Select<TEnd>(source, nameof(IGraphPathSegment<Node, IRelationship, Node>.EndNode));

        public IGraphQueryable<TRel> SelectRelationship<TRel>(IGraphQueryable<INode> source)
            where TRel : class, IRelationship
            => Select<TRel>(source, nameof(IGraphPathSegment<Node, IRelationship, Node>.Relationship));

        private IGraphQueryable<TResult> Select<TResult>(IGraphQueryable<INode> source, string propertyName)
        {
            var segmentType = ExtensionUtils.GetQueryableElementType(_expression.Type);
            var property = segmentType.GetProperty(propertyName)!;

            var parameter = Expression.Parameter(segmentType, "ps");
            var selector = Expression.Lambda(Expression.Property(parameter, property), parameter);

            var selectMethod = typeof(GraphQueryableExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(GraphQueryableExtensions.Select)
                    && m.GetGenericArguments().Length == 2
                    && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
                .MakeGenericMethod(segmentType, typeof(TResult));

            var expression = Expression.Call(null, selectMethod, _expression, selector);

            return source.Provider.CreateQuery<TResult>(expression);
        }
    }

    // ==================================================================================
    // Obsolete three-arg shims. Generic arity (3 vs 2) disambiguates these overloads from
    // the reshaped two-arg forms above; each delegates to the two-arg implementation by
    // relying on the covariant IGraphQueryable<out T> conversion from IGraphQueryable<TStartNode>
    // to IGraphQueryable<INode>. Kept for one release to ease migration (issue #94, "Option C").
    // ==================================================================================

    /// <summary>
    /// Obsolete three-arg form of <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode})"/>.
    /// </summary>
    [Obsolete("Use Traverse<TRel, TEnd>() instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.Traverse<TRelationship, TEndNode>();

    /// <summary>
    /// Obsolete three-arg form of <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode}, int)"/>.
    /// </summary>
    [Obsolete("Use Traverse<TRel, TEnd>(maxDepth) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        int maxDepth)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.Traverse<TRelationship, TEndNode>(maxDepth);

    /// <summary>
    /// Obsolete three-arg form of <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode}, int, int)"/>.
    /// </summary>
    [Obsolete("Use Traverse<TRel, TEnd>(minDepth, maxDepth) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        int minDepth,
        int maxDepth)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.Traverse<TRelationship, TEndNode>(minDepth, maxDepth);

    /// <summary>
    /// Obsolete three-arg form of <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode}, GraphTraversalDirection)"/>.
    /// </summary>
    [Obsolete("Use Traverse<TRel, TEnd>(direction) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        GraphTraversalDirection direction)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.Traverse<TRelationship, TEndNode>(direction);

    /// <summary>
    /// Obsolete three-arg form of <see cref="Traverse{TRel, TEnd}(IGraphQueryable{INode}, Func{GraphTraversalOptions, GraphTraversalOptions})"/>.
    /// </summary>
    [Obsolete("Use Traverse<TRel, TEnd>(configure) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        Func<GraphTraversalOptions, GraphTraversalOptions> configure)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.Traverse<TRelationship, TEndNode>(configure);

    /// <summary>
    /// Obsolete three-arg form of <see cref="ReverseTraverse{TRel, TEnd}(IGraphQueryable{INode})"/>.
    /// </summary>
    [Obsolete("Use ReverseTraverse<TRel, TEnd>() instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TEndNode> ReverseTraverse<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.ReverseTraverse<TRelationship, TEndNode>();

    /// <summary>
    /// Obsolete three-arg form of <see cref="TraverseRelationships{TRel, TEnd}(IGraphQueryable{INode})"/>.
    /// </summary>
    [Obsolete("Use TraverseRelationships<TRel, TEnd>() instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<TRelationship> TraverseRelationships<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.TraverseRelationships<TRelationship, TEndNode>();

    /// <summary>
    /// Obsolete three-arg form of <see cref="TraversePaths{TRel, TEnd}(IGraphQueryable{INode}, int, int)"/>.
    /// </summary>
    [Obsolete("Use TraversePaths<TRel, TEnd>(minDepth, maxDepth) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<IGraphPath> TraversePaths<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        int minDepth,
        int maxDepth)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.TraversePaths<TRelationship, TEndNode>(minDepth, maxDepth);

    /// <summary>
    /// Obsolete three-arg form of <see cref="TraversePaths{TRel, TEnd}(IGraphQueryable{INode}, Func{GraphTraversalOptions, GraphTraversalOptions})"/>.
    /// </summary>
    [Obsolete("Use TraversePaths<TRel, TEnd>(configure) instead - TStartNode is inferred from the source via covariance and no longer needs to be spelled out. This overload will be removed in a future release.")]
    public static IGraphQueryable<IGraphPath> TraversePaths<TStartNode, TRelationship, TEndNode>(
        this IGraphQueryable<TStartNode> source,
        Func<GraphTraversalOptions, GraphTraversalOptions> configure)
        where TStartNode : class, INode
        where TRelationship : class, IRelationship
        where TEndNode : class, INode
        => source.TraversePaths<TRelationship, TEndNode>(configure);
}
#pragma warning restore CS0618

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

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Neo4j implementation of the IGraphQueryProvider interface.
/// Handles query creation, expression tree translation, and execution against Neo4j.
/// </summary>
internal sealed class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _context;
    private readonly CypherEngine _cypherEngine;
    private readonly ILogger _logger;

    public GraphQueryProvider(GraphContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cypherEngine = new CypherEngine(_context);
        _logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>() ?? NullLogger<GraphQueryProvider>.Instance;

        _logger.LogInformation("GraphQueryProvider initialized for database '{DatabaseName}'", _context.DatabaseName);
    }

    /// <inheritdoc/>
    public IGraph Graph => _context.Graph;

    /// <inheritdoc/>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating query for type {ElementType} with expression: {Expression}",
            typeof(TElement).Name, expression);

        var queryContext = ExtractQueryContext(expression);

        // Determine the type of queryable to create based on the element type
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            _logger.LogDebug("Creating node query for type {NodeType}", typeof(TElement).Name);

            // Use reflection since TElement isn't constrained
            var graphNodeQueryableType = typeof(GraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IQueryable<TElement>)Activator.CreateInstance(
                graphNodeQueryableType,
                this,
                _context,
                queryContext,
                expression)!;
        }

        // Determine the type of queryable to create based on the element type
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            _logger.LogDebug("Creating node query for type {NodeType}", typeof(TElement).Name);

            // Use reflection since TElement isn't constrained
            var graphRelQueryableType = typeof(GraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IQueryable<TElement>)Activator.CreateInstance(
                graphRelQueryableType,
                this,
                _context,
                queryContext,
                expression)!;
        }

        _logger.LogDebug("Creating generic query for type {ElementType}", typeof(TElement).Name);
        return new GraphQueryable<TElement>(this, _context, queryContext, expression);
    }

    /// <inheritdoc/>
    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating non-generic query with expression: {Expression}", expression);

        var elementType = GetElementTypeFromExpression(expression)
            ?? throw new ArgumentException("Cannot determine element type from expression", nameof(expression));

        _logger.LogDebug("Determined element type: {ElementType}", elementType.Name);

        var createQueryMethod = typeof(GraphQueryProvider)
            .GetMethod(nameof(CreateQuery), [typeof(Expression)])!
            .MakeGenericMethod(elementType);

        return (IQueryable)createQueryMethod.Invoke(this, [expression])!;
    }

    /// <inheritdoc/>
    public IGraphNodeQueryable<TNode> CreateNodeQuery<TNode>(Expression expression) where TNode : INode
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating node query for type {NodeType} with expression: {Expression}",
            typeof(TNode).Name, expression);

        var queryContext = ExtractQueryContext(expression);
        return new GraphNodeQueryable<TNode>(this, _context, queryContext, expression);
    }

    /// <inheritdoc/>
    public IGraphRelationshipQueryable<TRel> CreateRelationshipQuery<TRel>(Expression expression)
        where TRel : IRelationship
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating relationship query for type {RelType} with expression: {Expression}",
            typeof(TRel).Name, expression);

        var queryContext = ExtractQueryContext(expression);
        return new GraphRelationshipQueryable<TRel>(this, _context, queryContext, expression);
    }

    /// <inheritdoc/>
    public IGraphTraversalQueryable<TSource, TRelationship, TTarget> CreateTraversalQuery<TSource, TRelationship, TTarget>(
    Expression sourceExpression)
    where TSource : INode
    where TRelationship : IRelationship
    where TTarget : INode
    {
        ArgumentNullException.ThrowIfNull(sourceExpression);

        _logger.LogDebug("Creating traversal query: {Source} -[{Relationship}]-> {Target}",
            typeof(TSource).Name, typeof(TRelationship).Name, typeof(TTarget).Name);

        var queryContext = ExtractQueryContext(sourceExpression);

        // The traversal expression should represent calling Traverse on the source
        // Since Traverse is an instance method on IGraphNodeQueryable, we need to create
        // a method call expression that represents: source.Traverse<TRelationship, TTarget>()
        var traverseMethod = typeof(IGraphNodeQueryable<TSource>)
            .GetMethod(nameof(IGraphNodeQueryable<TSource>.Traverse))!
            .MakeGenericMethod(typeof(TRelationship), typeof(TTarget));

        var traversalExpression = Expression.Call(
            sourceExpression,
            traverseMethod);

        return new GraphTraversalQueryable<TSource, TRelationship, TTarget>(
            this, _context, queryContext, traversalExpression, sourceExpression);
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public IGraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>> CreatePathSegmentQuery<TSource, TRel, TTarget>(
        Expression expression)
        where TSource : INode
        where TRel : IRelationship
        where TTarget : INode
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating path segment query: {Source} -[{Relationship}]-> {Target}",
            typeof(TSource).Name, typeof(TRel).Name, typeof(TTarget).Name);

        var queryContext = ExtractQueryContext(expression);

        // PathSegments is an instance method on IGraphNodeQueryable
        var pathSegmentsMethod = typeof(IGraphNodeQueryable<TSource>)
            .GetMethod(nameof(IGraphNodeQueryable<TSource>.PathSegments))!
            .MakeGenericMethod(typeof(TRel), typeof(TTarget));

        var pathSegmentExpression = Expression.Call(
            expression,
            pathSegmentsMethod);

        // Use the generic GraphQueryable<T> since we're returning IGraphQueryable<IGraphPathSegment<...>>
        return new GraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>>(
            this, _context, queryContext, pathSegmentExpression);
    }

    /// <inheritdoc/>
    public TResult Execute<TResult>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing synchronous query: {Expression}", expression);

        try
        {
            return ExecuteAsync<TResult>(expression, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query synchronously");
            throw;
        }
    }

    /// <inheritdoc/>
    public object? Execute(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing non-generic synchronous query: {Expression}", expression);

        var elementType = GetElementTypeFromExpression(expression)
            ?? throw new ArgumentException("Cannot determine element type from expression", nameof(expression));

        var executeMethod = typeof(GraphQueryProvider)
            .GetMethod(nameof(Execute), [typeof(Expression)])!
            .MakeGenericMethod(elementType);

        return executeMethod.Invoke(this, [expression]);
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing async query for result type {ResultType}: {Expression}",
            typeof(TResult).Name, expression);

        try
        {
            var queryContext = ExtractQueryContext(expression);

            _logger.LogDebug("Translating expression to Cypher");
            var cypherQuery = await _cypherEngine.ExpressionToCypherVisitor(expression, queryContext, cancellationToken);

            var result = await _cypherEngine.ExecuteAsync<TResult>(
                cypherQuery,
                queryContext,
                cancellationToken);

            _logger.LogDebug("Query executed successfully, returning result of type {ResultType}",
                result?.GetType().Name ?? "null");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing async query for type {ResultType}", typeof(TResult).Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<object?> ExecuteAsync(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing non-generic async query: {Expression}", expression);

        var elementType = GetElementTypeFromExpression(expression)
            ?? throw new ArgumentException("Cannot determine element type from expression", nameof(expression));

        _logger.LogDebug("Determined element type: {ElementType}", elementType.Name);

        var executeAsyncMethod = typeof(GraphQueryProvider)
            .GetMethod(nameof(ExecuteAsync), [typeof(Expression), typeof(CancellationToken)])!
            .MakeGenericMethod(elementType);

        var task = (Task)executeAsyncMethod.Invoke(this, [expression, cancellationToken])!;
        await task;

        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    // Helper methods

    private GraphQueryContext ExtractQueryContext(Expression expression)
    {
        _logger.LogDebug("Extracting query context from expression: {Expression}", expression);

        // Walk the expression tree to find the root queryable
        var rootQueryable = FindRootQueryable(expression);

        if (rootQueryable is not null)
        {
            _logger.LogDebug("Found existing query context");
            return rootQueryable.QueryContext;
        }

        // Create a new query context if we can't find one
        _logger.LogDebug("Creating new query context");
        return new GraphQueryContext();
    }

    private GraphQueryable? FindRootQueryable(Expression expression)
    {
        return expression switch
        {
            ConstantExpression { Value: GraphQueryable queryable } => queryable,

            MethodCallExpression { Arguments.Count: > 0 } mce =>
                FindRootQueryable(mce.Arguments[0]),

            MemberExpression me =>
                FindRootQueryable(me.Expression!),

            _ => null
        };
    }

    private static Type? GetElementTypeFromExpression(Expression expression)
    {
        var type = expression.Type;

        // Check if it's IQueryable<T> or IEnumerable<T>
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(IQueryable<>) ||
                genericTypeDef == typeof(IEnumerable<>) ||
                genericTypeDef == typeof(IGraphQueryable<>) ||
                genericTypeDef == typeof(IGraphNodeQueryable<>) ||
                genericTypeDef == typeof(IGraphRelationshipQueryable<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        // Check implemented interfaces
        var queryableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IQueryable<>) ||
                 i.GetGenericTypeDefinition() == typeof(IGraphQueryable<>)));

        return queryableInterface?.GetGenericArguments()[0];
    }
}
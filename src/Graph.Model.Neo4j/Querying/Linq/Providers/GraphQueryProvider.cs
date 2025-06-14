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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Linq.Helpers;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _graphContext;
    private readonly ILogger<GraphQueryProvider> _logger;

    public GraphQueryProvider(GraphContext context)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>()
            ?? NullLogger<GraphQueryProvider>.Instance;
    }

    public IGraph Graph => _graphContext.Graph;

    #region IQueryProvider Implementation

    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var elementType = TypeHelpers.GetElementType(expression.Type);

        try
        {
            return (IQueryable)GetType()
                .GetMethod(nameof(CreateQuery), 1, [typeof(Expression)])!
                .MakeGenericMethod(elementType)
                .Invoke(this, [expression])!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create query for type {elementType}", ex);
        }
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Check if this is a node type
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(GraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IQueryable<TElement>)Activator.CreateInstance(
                nodeQueryableType,
                this,
                _graphContext,
                expression)!;
        }

        // Check if this is a relationship type
        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relQueryableType = typeof(GraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IQueryable<TElement>)Activator.CreateInstance(
                relQueryableType,
                this,
                _graphContext,
                expression)!;
        }

        // For other types (projections, anonymous types, path segments, etc.)
        return new GraphQueryable<TElement>(this, _graphContext, expression);
    }

    public object? Execute(Expression expression)
    {
        return ExecuteInternal<object>(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return ExecuteInternal<TResult>(expression);
    }

    #endregion

    #region IGraphQueryProvider Implementation

    public IGraphRelationshipQueryable<TRel> CreateRelationshipQuery<TRel>(Expression expression)
        where TRel : IRelationship
    {
        return new GraphRelationshipQueryable<TRel>(this, _graphContext, expression);
    }

    public IGraphNodeQueryable<TNode> CreateNodeQuery<TNode>(Expression expression)
        where TNode : INode
    {
        return new GraphNodeQueryable<TNode>(this, _graphContext, expression);
    }

    public IGraphTraversalQueryable<TSource, TRelationship, TTarget> CreateTraversalQuery<TSource, TRelationship, TTarget>(
        Expression sourceExpression)
        where TSource : INode
        where TRelationship : IRelationship
        where TTarget : INode
    {
        return new GraphTraversalQueryable<TSource, TRelationship, TTarget>(this, _graphContext, sourceExpression);
    }

    public IGraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>> CreatePathSegmentQuery<TSource, TRel, TTarget>(
        Expression expression)
        where TSource : INode
        where TRel : IRelationship
        where TTarget : INode
    {
        return new GraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>>(this, _graphContext, expression);
    }

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<object?>(expression, cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
         Expression expression,
         CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing async query: {Expression}", expression);

        // For now, let's just create a placeholder implementation
        // This will be replaced when we implement the Cypher visitor
        await Task.Yield(); // Ensure async behavior

        // Extract the query context from the expression
        var queryContext = GraphQueryContext.FromExpression(expression);

        _logger.LogDebug("Query context created with transaction: {HasTransaction}",
            queryContext.Transaction != null);

        // TODO: Implement Cypher generation and execution
        // For now, return default values based on the result type
        if (typeof(TResult) == typeof(int) || typeof(TResult) == typeof(long))
        {
            return (TResult)(object)0;
        }

        if (typeof(TResult) == typeof(bool))
        {
            return (TResult)(object)false;
        }

        if (typeof(TResult).IsGenericType &&
            typeof(TResult).GetGenericTypeDefinition() == typeof(List<>))
        {
            var listType = typeof(List<>).MakeGenericType(typeof(TResult).GetGenericArguments()[0]);
            return (TResult)Activator.CreateInstance(listType)!;
        }

        return default!;
    }

    #endregion

    private TResult ExecuteInternal<TResult>(Expression expression)
    {
        return ExecuteAsync<TResult>(expression, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}

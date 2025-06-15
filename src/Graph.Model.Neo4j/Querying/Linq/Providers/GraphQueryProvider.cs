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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _graphContext;
    private readonly ILogger<GraphQueryProvider> _logger;
    private readonly CypherEngine _cypherEngine;
    public GraphQueryProvider(GraphContext context)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>()
            ?? NullLogger<GraphQueryProvider>.Instance;
        _cypherEngine = new CypherEngine(context.EntityFactory, context.LoggerFactory);
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

    // In GraphQueryProvider.cs, update the ExecuteAsync method:

    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing async query for result type: {ResultType}", typeof(TResult).Name);

        GraphTransaction? transaction = null;
        var shouldDisposeTransaction = false;

        try
        {
            // Extract transaction if present in the expression
            transaction = ExtractTransaction(expression);

            // If no transaction was provided, create one for this query
            if (transaction is null)
            {
                transaction = await TransactionHelpers.GetOrCreateTransactionAsync(
                    _graphContext,
                    transaction: null,
                    isReadOnly: true); // Queries are read-only by default
                shouldDisposeTransaction = true;
                _logger.LogDebug("Created read-only transaction for query execution");
            }

            // Execute using the CypherEngine
            var results = await _cypherEngine.ExecuteAsync<TResult>(
                expression,
                transaction,
                cancellationToken);

            // Handle single result expectations
            if (IsSingleResultExpected(expression))
            {
                var singleResult = results.FirstOrDefault();
                if (singleResult is null && !expression.ToString().Contains("OrDefault"))
                {
                    throw new InvalidOperationException("Sequence contains no elements");
                }
                return singleResult!;
            }

            // Return collection results
            if (results is TResult typedResults)
            {
                return typedResults;
            }

            // This shouldn't happen, but let's be safe
            throw new InvalidOperationException($"Cannot convert results to {typeof(TResult).Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");

            // If we created the transaction, try to roll it back
            if (shouldDisposeTransaction && transaction != null)
            {
                try
                {
                    await transaction.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction after error");
                }
            }

            throw;
        }
        finally
        {
            // Clean up the transaction if we created it
            if (shouldDisposeTransaction && transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private GraphTransaction? ExtractTransaction(Expression expression)
    {
        var visitor = new TransactionExtractionVisitor();
        visitor.Visit(expression);

        // Check if we found multiple transactions - that's not allowed!
        if (visitor.Transactions.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple transactions found in query expression. Found {visitor.Transactions.Count} transactions, but only one is allowed per query.");
        }

        // Return the single transaction if found, or null if none
        return visitor.Transactions.SingleOrDefault();
    }

    private bool IsSingleResultExpected(Expression expression)
    {
        if (expression is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            return methodName is "First" or "FirstOrDefault" or "Single" or "SingleOrDefault"
                or "Last" or "LastOrDefault";
        }

        return false;
    }

    #endregion

    private TResult ExecuteInternal<TResult>(Expression expression)
    {
        return ExecuteAsync<TResult>(expression, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}

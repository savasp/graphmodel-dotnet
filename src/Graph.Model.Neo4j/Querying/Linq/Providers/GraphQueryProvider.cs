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
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides LINQ query execution capabilities for graph operations.
/// </summary>
internal sealed class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _graphContext;
    private readonly GraphTransaction _transaction;
    private readonly ILogger<GraphQueryProvider> _logger;
    private readonly CypherEngine _cypherEngine;

    public GraphQueryProvider(GraphContext context, GraphTransaction transaction)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>() ?? NullLogger<GraphQueryProvider>.Instance;
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

    public IGraphQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Use the transaction from the provider context - no need to extract from expression
        var transaction = _transaction;

        // Determine the queryable type based on TElement
        if (typeof(INode).IsAssignableFrom(typeof(TElement)))
        {
            var nodeQueryableType = typeof(GraphNodeQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(
                nodeQueryableType,
                this,
                _graphContext,
                transaction,
                expression)!;
        }

        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            var relQueryableType = typeof(GraphRelationshipQueryable<>).MakeGenericType(typeof(TElement));
            return (IGraphQueryable<TElement>)Activator.CreateInstance(
                relQueryableType,
                this,
                _graphContext,
                transaction,
                expression)!;
        }

        // For other types (projections, anonymous types, etc.)
        return new GraphQueryable<TElement>(this, _graphContext, transaction, expression);
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

    public Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<object?>(expression, cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing async query for result type: {ResultType}", typeof(TResult).Name);
        _logger.LogDebug("Expression type: {ExpressionType}", expression.Type.Name);

        // Log the expression tree for debugging
        LogExpressionTree(expression);

        try
        {
            // Use the transaction from the provider context
            var transaction = _transaction;

            var result = await TransactionHelpers.ExecuteInTransactionAsync(
                _graphContext,
                transaction,
                tx =>
                {
                    // Execute using the CypherEngine
                    return _cypherEngine.ExecuteAsync<TResult>(
                        expression,
                        tx,
                        cancellationToken);
                },
                "Error executing query",
                _logger);

            return result!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");

            throw;
        }
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

    private TResult ExecuteInternal<TResult>(Expression expression)
    {
        return ExecuteAsync<TResult>(expression, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
    {
        return CreateQuery<TElement>(expression);
    }

    private void LogExpressionTree(Expression expression, int depth = 0)
    {
        var indent = new string(' ', depth * 2);

        if (expression is MethodCallExpression methodCall)
        {
            _logger.LogDebug("{Indent}Method: {Method} from {DeclaringType}",
                indent, methodCall.Method.Name, methodCall.Method.DeclaringType?.Name);

            foreach (var arg in methodCall.Arguments)
            {
                LogExpressionTree(arg, depth + 1);
            }
        }
        else if (expression is ConstantExpression constant)
        {
            _logger.LogDebug("{Indent}Constant: {Type}",
                indent, constant.Value?.GetType().Name ?? "null");
        }
        // More expression types can be added here as needed
        else
        {
            _logger.LogDebug("{Indent}Unhandled expression type: {Type}",
                indent, expression.GetType().Name);
        }
    }
}

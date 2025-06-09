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
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Cypher;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Neo4j implementation of the IGraphQueryProvider interface.
/// Handles query creation, expression tree translation, and execution against Neo4j.
/// </summary>
internal sealed class GraphQueryProvider : IGraphQueryProvider
{
    private readonly GraphContext _graphContext;
    private readonly CypherEngine _cypherEngine;
    private readonly ILogger _logger;

    private static readonly MethodInfo CreateNodeQueryMethod = typeof(GraphQueryProvider)
        .GetMethod(nameof(CreateNodeQuery), BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly MethodInfo CreateRelationshipQueryMethod = typeof(GraphQueryProvider)
        .GetMethod(nameof(CreateRelationshipQuery), BindingFlags.Public | BindingFlags.Instance)!;

    public GraphQueryProvider(GraphContext context)
    {
        _graphContext = context ?? throw new ArgumentNullException(nameof(context));
        _cypherEngine = new CypherEngine(_graphContext);
        _logger = context.LoggerFactory?.CreateLogger<GraphQueryProvider>() ?? NullLogger<GraphQueryProvider>.Instance;

        _logger.LogInformation("GraphQueryProvider initialized for database '{DatabaseName}'", _graphContext.DatabaseName);
    }

    /// <inheritdoc/>
    public IGraph Graph => _graphContext.Graph;

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
            return (IQueryable<TElement>)CreateNodeQueryMethod.MakeGenericMethod(typeof(TElement)).Invoke(this, [expression])!;
        }

        // Determine the type of queryable to create based on the element type
        if (typeof(IRelationship).IsAssignableFrom(typeof(TElement)))
        {
            _logger.LogDebug("Creating relationship query for type {RelType}", typeof(TElement).Name);
            return (IQueryable<TElement>)CreateRelationshipQueryMethod.MakeGenericMethod(typeof(TElement)).Invoke(this, [expression])!;
        }

        _logger.LogDebug("Creating generic query for type {ElementType}", typeof(TElement).Name);
        return new GraphQueryable<TElement>(this, _graphContext, queryContext, expression);
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
        return new GraphNodeQueryable<TNode>(this, _graphContext, queryContext, expression);
    }

    /// <inheritdoc/>
    public IGraphRelationshipQueryable<TRel> CreateRelationshipQuery<TRel>(Expression expression)
        where TRel : IRelationship
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Creating relationship query for type {RelType} with expression: {Expression}",
            typeof(TRel).Name, expression);

        var queryContext = ExtractQueryContext(expression);
        return new GraphRelationshipQueryable<TRel>(this, _graphContext, queryContext, expression);
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
            this, _graphContext, queryContext, traversalExpression, sourceExpression);
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
            this, _graphContext, queryContext, pathSegmentExpression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Executing synchronous query: {Expression}", expression);

        try
        {
            // For synchronous execution, we'll still use the async path internally
            // This ensures consistent transaction handling
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

        var (transaction, shouldDisposeTransaction) = await ExtractTransactionFromExpression(expression, cancellationToken);

        try
        {
            var queryContext = ExtractQueryContext(expression);

            queryContext.Transaction = transaction;

            _logger.LogDebug("Translating expression to Cypher");
            var cypherQuery = _cypherEngine.ExpressionToCypherVisitor(expression, queryContext);

            var result = await _cypherEngine.ExecuteAsync<TResult>(
                cypherQuery,
                transaction,
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
        finally
        {
            if (shouldDisposeTransaction && transaction != null)
            {
                _logger.LogDebug("Disposing transaction created for query execution");
                await transaction.DisposeAsync();
            }
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

    // Remove the IAsyncTransaction version and keep only this one:

    private async Task<(GraphTransaction transaction, bool shouldDisposeTransaction)> ExtractTransactionFromExpression(
        Expression expression,
        CancellationToken cancellationToken)
    {
        var transactions = new HashSet<GraphTransaction>();
        ExtractTransactionsFromExpressionTree(expression, transactions);

        if (transactions.Count > 1)
        {
            _logger.LogError("Multiple transactions found in expression tree. Count: {Count}", transactions.Count);
            throw new InvalidOperationException(
                $"Multiple transactions ({transactions.Count}) found in query expression. Only one transaction is allowed per query.");
        }

        if (transactions.Count == 1)
        {
            var transaction = transactions.First();
            _logger.LogDebug("Found transaction in expression tree: {TransactionId}", transaction.GetHashCode());
            return (transaction, shouldDisposeTransaction: false);
        }

        // No transaction in expression tree - use TransactionHelpers to create a read-only one
        _logger.LogDebug("No transaction found in expression tree, creating read-only transaction");

        // Use TransactionHelpers.GetOrCreateTransactionAsync if it exists
        var newTransaction = await TransactionHelpers.GetOrCreateTransactionAsync(
            _graphContext,
            transaction: null);

        return (newTransaction, shouldDisposeTransaction: true);
    }

    private void ExtractTransactionsFromExpressionTree(Expression expression, HashSet<GraphTransaction> transactions)
    {
        switch (expression)
        {
            case MethodCallExpression mce when mce.Method.Name == "WithTransaction":
                // The transaction should be in the first argument
                if (mce.Arguments.Count > 0)
                {
                    // Handle both constant and member expressions
                    var transactionValue = GetValueFromExpression(mce.Arguments[0]);
                    if (transactionValue is GraphTransaction transaction)
                    {
                        transactions.Add(transaction);
                    }
                }
                // Continue walking the tree with remaining arguments
                foreach (var arg in mce.Arguments.Skip(1))
                {
                    ExtractTransactionsFromExpressionTree(arg, transactions);
                }
                // Also check the object if it's an instance method
                if (mce.Object != null)
                {
                    ExtractTransactionsFromExpressionTree(mce.Object, transactions);
                }
                break;

            case MethodCallExpression mce:
                // Check all arguments
                foreach (var arg in mce.Arguments)
                {
                    ExtractTransactionsFromExpressionTree(arg, transactions);
                }
                if (mce.Object != null)
                {
                    ExtractTransactionsFromExpressionTree(mce.Object, transactions);
                }
                break;

            case BinaryExpression be:
                ExtractTransactionsFromExpressionTree(be.Left, transactions);
                ExtractTransactionsFromExpressionTree(be.Right, transactions);
                break;

            case UnaryExpression ue when ue.Operand != null:
                ExtractTransactionsFromExpressionTree(ue.Operand, transactions);
                break;

            case LambdaExpression le:
                ExtractTransactionsFromExpressionTree(le.Body, transactions);
                break;

            case MemberExpression me when me.Expression != null:
                ExtractTransactionsFromExpressionTree(me.Expression, transactions);
                break;

            case NewExpression ne:
                // Check constructor arguments
                foreach (var arg in ne.Arguments)
                {
                    ExtractTransactionsFromExpressionTree(arg, transactions);
                }
                break;

            case InvocationExpression ie:
                ExtractTransactionsFromExpressionTree(ie.Expression, transactions);
                foreach (var arg in ie.Arguments)
                {
                    ExtractTransactionsFromExpressionTree(arg, transactions);
                }
                break;

            case ConditionalExpression ce:
                ExtractTransactionsFromExpressionTree(ce.Test, transactions);
                ExtractTransactionsFromExpressionTree(ce.IfTrue, transactions);
                ExtractTransactionsFromExpressionTree(ce.IfFalse, transactions);
                break;

            case ConstantExpression { Value: GraphQueryable queryable }:
                // Check if the queryable has a transaction in its context
                if (queryable.QueryContext.Transaction != null)
                {
                    transactions.Add(queryable.QueryContext.Transaction);
                }
                break;
        }
    }

    // Helper method to extract values from expressions (handles constants, fields, properties)
    private static object? GetValueFromExpression(Expression expression)
    {
        return expression switch
        {
            ConstantExpression ce => ce.Value,
            MemberExpression me => GetMemberValue(me),
            _ => null
        };
    }

    private static object? GetMemberValue(MemberExpression memberExpression)
    {
        // Get the object that contains the member
        var objectMember = Expression.Convert(memberExpression, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }
}

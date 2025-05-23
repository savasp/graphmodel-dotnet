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

using System.Collections;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

public class Neo4jQueryProvider(
    Neo4jGraphProvider provider,
    GraphOperationOptions options,
    Microsoft.Extensions.Logging.ILogger? logger,
    string databaseName,
    IGraphTransaction? transaction,
    Type? rootType = null) : IQueryProvider
{
    private readonly Neo4jGraphProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly Microsoft.Extensions.Logging.ILogger? _logger = logger;
    private readonly string _databaseName = databaseName;
    private readonly Type _rootType = rootType ?? typeof(object);
    private readonly Type _elementType = rootType ?? typeof(object);
    private readonly IGraphTransaction? _transaction = transaction;
    private readonly GraphOperationOptions _options = options;

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? _elementType;
        var queryableType = typeof(Neo4jQueryable<>).MakeGenericType(elementType);

        // Pass options to the new queryable
        return (IQueryable)Activator.CreateInstance(
            queryableType,
            this,
            expression,
            _transaction,
            _options)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new Neo4jQueryable<TElement>(this, _options, expression, _transaction);
    }

    public object? Execute(Expression expression)
    {
        var elementType = GetElementTypeFromExpression(expression) ?? _elementType;
        var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
        var cypher = visitor.Translate(expression);

        // Get options from the expression if available
        var options = ExtractOptionsFromExpression(expression) ?? _options;

        var result = visitor.ExecuteQuery(cypher, elementType);

        // Apply traversal depth if options are specified
        if (options is { TraversalDepth: > 0 } && result is IEnumerable enumerable && result is not string)
        {
            ApplyTraversalDepthSync(enumerable, options, elementType);
        }

        return result;
    }

    public TResult Execute<TResult>(Expression expression)
    {
        var elementType = typeof(TResult);
        // If TResult is IEnumerable<T>, get T
        bool isEnumerableResult = false;
        if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = elementType.GetGenericArguments()[0];
            isEnumerableResult = true;
        }

        var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
        var cypher = visitor.Translate(expression);

        // Get options from the expression if available
        var options = ExtractOptionsFromExpression(expression) ?? _options;

        var result = visitor.ExecuteQuery(cypher, elementType);

        // Apply traversal depth if options are specified
        if (options is { TraversalDepth: > 0 } && result is IEnumerable enumerable && result is not string)
        {
            ApplyTraversalDepthSync(enumerable, options, elementType);
        }

        if (!isEnumerableResult || typeof(TResult) == typeof(string))
        {
            if (result is System.Collections.IEnumerable enumerableRes && result is not string)
            {
                var enumerator = enumerableRes.GetEnumerator();
                if (enumerator.MoveNext())
                    return (TResult)enumerator.Current!;
                return default!;
            }
            return (TResult)result!;
        }

        // Always return the sequence for IEnumerable
        if (result is System.Collections.IEnumerable enumerableResult && result is not string)
        {
            return (TResult)result!;
        }
        else if (result is not null)
        {
            // Wrap single value in a list
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(result);
            return (TResult)list;
        }
        else
        {
            // Return empty list
            var listType = typeof(List<>).MakeGenericType(elementType);
            return (TResult)Activator.CreateInstance(listType)!;
        }
    }

    private static Type? GetElementTypeFromExpression(Expression expression) => expression.Type switch
    {
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IQueryable<>)
            => type.GetGenericArguments()[0],
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            => type.GetGenericArguments()[0],
        _ => null
    };

    private GraphOperationOptions? ExtractOptionsFromExpression(Expression expression)
    {
        // Walk up the expression tree to find the root queryable
        var current = expression;
        while (current is not null)
        {
            if (current is ConstantExpression { Value: Neo4jQueryable<object> queryable })
            {
                return queryable.Options;
            }

            if (current is MethodCallExpression methodCall && methodCall.Arguments.Count > 0)
            {
                // Check first argument (usually the source)
                current = methodCall.Arguments[0];
            }
            else if (current is UnaryExpression unary)
            {
                current = unary.Operand;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private void ApplyTraversalDepthSync(IEnumerable results, GraphOperationOptions options, Type elementType)
    {
        // Convert to async and wait
        var task = ApplyTraversalDepthAsync(results, options, elementType);
        task.GetAwaiter().GetResult();
    }

    private async Task ApplyTraversalDepthAsync(IEnumerable results, GraphOperationOptions options, Type elementType)
    {
        var (session, tx) = await _provider.GetOrCreateTransaction(_transaction);
        try
        {
            var processedNodes = new HashSet<string>();

            foreach (var item in results)
            {
                switch (item)
                {
                    case Model.INode node:
                        await _provider.LoadNodeRelationships(node, options, tx, currentDepth: 0, processedNodes);
                        break;
                    case Model.IRelationship relationship:
                        await _provider.LoadRelationshipNodes(relationship, options, tx);
                        break;
                }
            }
        }
        finally
        {
            if (_transaction == null)
            {
                await session.CloseAsync();
            }
        }
    }

    // Async versions for async LINQ operations
    public async Task<object?> ExecuteAsync(Expression expression)
    {
        var elementType = GetElementTypeFromExpression(expression) ?? _elementType;
        var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
        var cypher = visitor.Translate(expression);

        // Get options from the expression if available
        var options = ExtractOptionsFromExpression(expression) ?? _options;

        var result = await visitor.ExecuteQueryAsync(cypher, elementType);

        // Apply traversal depth if options are specified
        if (options is { TraversalDepth: > 0 } && result is IEnumerable enumerable && result is not string)
        {
            await ApplyTraversalDepthAsync(enumerable, options, elementType);
        }

        return result;
    }

    public async Task<TResult> ExecuteAsync<TResult>(Expression expression)
    {
        var elementType = typeof(TResult);
        // If TResult is IEnumerable<T>, get T
        bool isEnumerableResult = false;
        if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = elementType.GetGenericArguments()[0];
            isEnumerableResult = true;
        }

        var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
        var cypher = visitor.Translate(expression);

        // Get options from the expression if available
        var options = ExtractOptionsFromExpression(expression) ?? _options;

        var result = await visitor.ExecuteQueryAsync(cypher, elementType);

        // Apply traversal depth if options are specified
        if (options is { TraversalDepth: > 0 } && result is IEnumerable enumerable && result is not string)
        {
            await ApplyTraversalDepthAsync(enumerable, options, elementType);
        }

        if (!isEnumerableResult || typeof(TResult) == typeof(string))
        {
            if (result is System.Collections.IEnumerable enumerableRes && result is not string)
            {
                var enumerator = enumerableRes.GetEnumerator();
                if (enumerator.MoveNext())
                    return (TResult)enumerator.Current!;
                return default!;
            }
            return (TResult)result!;
        }

        // Always return the sequence for IEnumerable
        if (result is System.Collections.IEnumerable enumerableResult && result is not string)
        {
            return (TResult)result!;
        }
        else if (result is not null)
        {
            // Wrap single value in a list
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(result);
            return (TResult)list;
        }
        else
        {
            // Return empty list
            var listType = typeof(List<>).MakeGenericType(elementType);
            return (TResult)Activator.CreateInstance(listType)!;
        }
    }
}
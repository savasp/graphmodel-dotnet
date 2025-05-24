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

namespace Cvoya.Graph.Provider.Neo4j.Linq;

internal class Neo4jQueryProvider(
    Neo4jGraphProvider provider,
    GraphOperationOptions options,
    Microsoft.Extensions.Logging.ILogger? logger,
    IGraphTransaction? transaction,
    Type? rootType = null) : IQueryProvider
{
    private readonly Neo4jGraphProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly Microsoft.Extensions.Logging.ILogger? _logger = logger;
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

        var result = visitor.ExecuteQuery(cypher, elementType);

        // Apply traversal depth if options are specified
        if (_options.TraversalDepth > 0 && result is IEnumerable enumerable && result is not string)
        {
            ApplyTraversalDepthSync(enumerable, _options, elementType);
        }

        return result;
    }

    public TResult Execute<TResult>(Expression expression)
    {
        var resultType = typeof(TResult);
        var elementType = resultType;

        // Check if TResult is an enumerable type
        bool isEnumerableResult = false;
        if (resultType.IsGenericType)
        {
            var genericDef = resultType.GetGenericTypeDefinition();
            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(ICollection<>))
            {
                elementType = resultType.GetGenericArguments()[0];
                isEnumerableResult = true;
            }
        }
        else if (resultType.IsArray)
        {
            elementType = resultType.GetElementType()!;
            isEnumerableResult = true;
        }
        // Also check if it implements IEnumerable<T>
        else if (resultType != typeof(string))
        {
            var enumerableInterface = resultType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerableInterface != null)
            {
                elementType = enumerableInterface.GetGenericArguments()[0];
                isEnumerableResult = true;
            }
        }

        var lastSelect = GetLastSelect(expression);

        // Check if we have TraversalDepth > 0 and a Select projection
        if (_options.TraversalDepth > 0 && lastSelect != null)
        {
            // Execute without projection first to load relationships
            var expressionWithoutProjection = RemoveLastSelect(expression);

            // Get the original entity type from the expression without projection
            var originalElementType = GetElementTypeFromExpression(expressionWithoutProjection);
            if (originalElementType == null)
            {
                // If we can't determine it from the expression, use the root type
                originalElementType = _rootType;
            }

            var visitor = new Neo4jExpressionVisitor(_provider, _rootType, originalElementType, _transaction);
            var cypher = visitor.Translate(expressionWithoutProjection);

            var result = visitor.ExecuteQuery(cypher, originalElementType);

            // Apply traversal depth to load relationships
            if (_options.TraversalDepth > 0)
            {
                if (result is IEnumerable enumerable && result is not string)
                {
                    ApplyTraversalDepthSync(enumerable, _options, originalElementType);
                }
                else if (result != null)
                {
                    // Handle single entity - create a temporary list
                    var tempList = new List<object> { result };
                    ApplyTraversalDepthSync(tempList, _options, originalElementType);
                }
            }

            // Now apply the projection in memory
            if (lastSelect != null)
            {
                result = ApplyProjection(result!, lastSelect, originalElementType);
            }

            // Handle the result based on expected return type
            return HandleResult<TResult>(result, resultType, elementType, isEnumerableResult);
        }
        else
        {
            // Normal execution path (unchanged)
            var visitor = new Neo4jExpressionVisitor(_provider, _rootType, elementType, _transaction);
            var cypher = visitor.Translate(expression);

            var result = visitor.ExecuteQuery(cypher, elementType);

            // Check if this was a grouping query
            if (visitor.IsGroupingQuery)
            {
                return (TResult)result!;
            }

            // Apply traversal depth if options are specified
            if (_options.TraversalDepth > 0 && result is IEnumerable enumerable && result is not string)
            {
                ApplyTraversalDepthSync(enumerable, _options, elementType);
            }

            return HandleResult<TResult>(result, resultType, elementType, isEnumerableResult);
        }
    }

    private TResult HandleResult<TResult>(object? result, Type resultType, Type elementType, bool isEnumerableResult)
    {
        // Handle scalar results (Count, Any, All, First, Single, etc.)
        if (!isEnumerableResult || resultType == typeof(string))
        {
            // Handle type conversions for scalar results
            if (result != null && result.GetType() != resultType)
            {
                // Special handling for numeric conversions
                if (resultType == typeof(int) && result is long longValue)
                {
                    return (TResult)(object)Convert.ToInt32(longValue);
                }
                else if (resultType == typeof(int?) && result is long longValue2)
                {
                    return (TResult)(object)Convert.ToInt32(longValue2);
                }

                // Try general conversion
                try
                {
                    return (TResult)Convert.ChangeType(result, resultType);
                }
                catch
                {
                    // If conversion fails, try direct cast
                    return (TResult)result;
                }
            }

            if (result is System.Collections.IEnumerable enumerableRes && result is not string)
            {
                var enumerator = enumerableRes.GetEnumerator();
                if (enumerator.MoveNext())
                    return (TResult)enumerator.Current!;
                return default!;
            }
            return (TResult)result!;
        }

        // Handle enumerable results
        if (result is System.Collections.IEnumerable enumerableResult && result is not string)
        {
            // If the result is already the correct type, return it
            if (result.GetType() == resultType || resultType.IsAssignableFrom(result.GetType()))
            {
                return (TResult)result;
            }

            // If we need to convert to array
            if (resultType.IsArray)
            {
                var list = (System.Collections.IList)result;
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return (TResult)(object)array;
            }

            // Otherwise return as is (it should be a List<T> which implements IEnumerable<T>)
            return (TResult)result;
        }
        else if (result is not null)
        {
            // Wrap single value in a list
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(result);
            return (TResult)(object)list;
        }
        else
        {
            // Return empty list
            var listType = typeof(List<>).MakeGenericType(elementType);
            return (TResult)Activator.CreateInstance(listType)!;
        }
    }

    private MethodCallExpression? GetLastSelect(Expression expression)
    {
        MethodCallExpression? lastSelect = null;

        var current = expression;
        while (current is MethodCallExpression method)
        {
            if (method.Method.Name == "Select")
            {
                lastSelect = method;
            }

            if (method.Arguments.Count > 0)
            {
                current = method.Arguments[0];
            }
            else
            {
                break;
            }
        }

        return lastSelect;
    }

    private Expression RemoveLastSelect(Expression expression)
    {
        // We need to rebuild the expression tree without the Select
        var result = RemoveSelectFromTree(expression);

        return result;
    }

    private Expression RemoveSelectFromTree(Expression expression)
    {
        if (expression is MethodCallExpression method)
        {
            if (method.Method.Name == "Select")
            {
                // Found the Select - return its source (skip this method call)
                return method.Arguments[0];
            }
            else if (method.Arguments.Count > 0)
            {
                // For other methods, recurse on the source and rebuild if needed
                var newSource = RemoveSelectFromTree(method.Arguments[0]);
                if (newSource != method.Arguments[0])
                {
                    // The source changed, so we need to rebuild this method call
                    // But we need to handle generic method definitions properly

                    if (method.Method.IsGenericMethod)
                    {
                        // Get the element type from the new source
                        var newSourceElementType = GetElementTypeFromExpression(newSource);
                        if (newSourceElementType != null)
                        {
                            // Rebuild the generic method with the correct type
                            var genericMethodDef = method.Method.GetGenericMethodDefinition();
                            var newMethod = genericMethodDef.MakeGenericMethod(newSourceElementType);

                            var newArgs = new Expression[method.Arguments.Count];
                            newArgs[0] = newSource;
                            for (int i = 1; i < method.Arguments.Count; i++)
                            {
                                newArgs[i] = method.Arguments[i];
                            }

                            return Expression.Call(method.Object, newMethod, newArgs);
                        }
                    }

                    // Fallback: if not generic or type inference failed, try original approach
                    try
                    {
                        var newArgs = new Expression[method.Arguments.Count];
                        newArgs[0] = newSource;
                        for (int i = 1; i < method.Arguments.Count; i++)
                        {
                            newArgs[i] = method.Arguments[i];
                        }
                        return Expression.Call(method.Object, method.Method, newArgs);
                    }
                    catch
                    {
                        // If rebuilding fails, just return the new source
                        // This means we'll lose the outer method call, but at least the query will work
                        return newSource;
                    }
                }
            }
        }

        return expression;
    }

    private object ApplyProjection(object source, MethodCallExpression selectExpression, Type sourceElementType)
    {
        var lambda = ExtractLambda(selectExpression.Arguments[1]);
        if (lambda != null)
        {
            var compiled = lambda.Compile();

            if (source is IEnumerable enumerable && source is not string)
            {
                // Handle enumerable source - apply projection to each item
                var results = new List<object>();
                foreach (var item in enumerable)
                {
                    var projectedItem = compiled.DynamicInvoke(item);
                    if (projectedItem != null)
                    {
                        results.Add(projectedItem);
                    }
                }

                // If we only have one result and this is a single-item query (like First), return just that item
                if (results.Count == 1 && IsLimitOneQuery(selectExpression))
                {
                    return results[0];
                }

                // Create typed list
                var resultType = lambda.Body.Type;
                var listType = typeof(List<>).MakeGenericType(resultType);
                var typedList = (System.Collections.IList)Activator.CreateInstance(listType)!;
                foreach (var item in results)
                {
                    typedList.Add(item);
                }
                return typedList;
            }
            else
            {
                // Handle single item - apply projection directly
                var result = compiled.DynamicInvoke(source);
                return result!;
            }
        }

        return source;
    }

    private bool IsLimitOneQuery(Expression expression)
    {
        // Check if this is a query that should return a single item (First, Single, etc.)
        var current = expression;
        while (current is MethodCallExpression method)
        {
            if (method.Method.Name is "First" or "FirstOrDefault" or "Single" or "SingleOrDefault")
            {
                return true;
            }
            if (method.Arguments.Count > 0)
            {
                current = method.Arguments[0];
            }
            else
            {
                break;
            }
        }
        return false;
    }

    private LambdaExpression? ExtractLambda(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambda => lambda,
            UnaryExpression unary when unary.NodeType == ExpressionType.Quote => unary.Operand as LambdaExpression,
            _ => null
        };
    }

    private static Type? GetElementTypeFromExpression(Expression expression) => expression.Type switch
    {
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IQueryable<>)
            => type.GetGenericArguments()[0],
        { IsGenericType: true } type when type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            => type.GetGenericArguments()[0],
        _ => null
    };

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

        var result = await visitor.ExecuteQueryAsync(cypher, elementType);

        // Apply traversal depth if options are specified
        if (_options.TraversalDepth > 0 && result is IEnumerable enumerable && result is not string)
        {
            await ApplyTraversalDepthAsync(enumerable, _options, elementType);
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

        var result = await visitor.ExecuteQueryAsync(cypher, elementType);

        // Apply traversal depth if options are specified
        if (_options.TraversalDepth > 0 && result is IEnumerable enumerable && result is not string)
        {
            await ApplyTraversalDepthAsync(enumerable, _options, elementType);
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
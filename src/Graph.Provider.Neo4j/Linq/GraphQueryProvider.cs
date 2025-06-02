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

internal class GraphQueryProvider(
    Neo4jGraphProvider provider,
    Microsoft.Extensions.Logging.ILogger? logger,
    IGraphTransaction? transaction,
    Type? rootType = null) : IQueryProvider
{
    private readonly Neo4jGraphProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly Microsoft.Extensions.Logging.ILogger? _logger = logger;
    private readonly Type _rootType = rootType ?? typeof(object);
    private readonly Type _elementType = rootType ?? typeof(object);
    private readonly IGraphTransaction? _transaction = transaction;

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? _elementType;
        var queryableType = typeof(GraphQueryable<>).MakeGenericType(elementType);

        // Get the internal constructor that takes 4 parameters
        var constructor = queryableType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(GraphQueryProvider), typeof(Expression), typeof(IGraphTransaction), typeof(IGraphQueryContext) },
            null);

        if (constructor == null)
        {
            throw new InvalidOperationException($"Could not find appropriate constructor for {queryableType.Name}");
        }

        return (IQueryable)constructor.Invoke(new object?[] {
            this,          // provider
            expression,    // expression
            _transaction,  // transaction
            new GraphQueryContext() // context
        });
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        // We need to handle the case where TElement might not be a class
        // Use reflection to create the appropriate GraphQueryable<T> instance
        var elementType = typeof(TElement);
        if (!elementType.IsClass && !elementType.IsInterface)
        {
            throw new InvalidOperationException($"Type {elementType.Name} must be a reference type to be used in graph queries");
        }

        // Use reflection to create GraphQueryable<TElement> since we can't guarantee TElement : class constraint
        var queryableType = typeof(GraphQueryable<>).MakeGenericType(elementType);

        // Get the internal constructor that takes 4 parameters
        var constructor = queryableType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(GraphQueryProvider), typeof(Expression), typeof(IGraphTransaction), typeof(IGraphQueryContext) },
            null);

        if (constructor == null)
        {
            throw new InvalidOperationException($"Could not find appropriate constructor for {queryableType.Name}");
        }

        var queryable = constructor.Invoke(new object?[] {
            this,          // provider
            expression,    // expression
            _transaction,  // transaction
            new GraphQueryContext() // context
        });

        // Return the GraphQueryable which implements both IQueryable<T> and IGraphQueryable<T>
        return (IQueryable<TElement>)queryable;
    }

    public object? Execute(Expression expression)
    {
        var elementType = GetElementTypeFromExpression(expression) ?? _elementType;

        // Use the new CypherExpressionBuilder which has complete traversal support
        var (cypher, parameters, context) = CypherExpressionBuilder.BuildGraphQuery(expression, elementType, _provider);

        // Execute the query using the new CypherExpressionBuilder execution method
        var nonNullableParams = parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? (object)DBNull.Value);
        var result = CypherExpressionBuilder.ExecuteQuery(cypher, nonNullableParams, elementType, _provider, context, _transaction);

        return result;
    }

    public TResult Execute<TResult>(Expression expression)
    {
        var resultType = typeof(TResult);
        var elementType = resultType;

        // Check if TResult is an enumerable type
        bool isEnumerableResult = false;

        // First check if it's a string (which implements IEnumerable<char> but should be treated as scalar)
        if (resultType == typeof(string))
        {
            isEnumerableResult = false;
        }
        // Check for array types
        else if (resultType.IsArray)
        {
            elementType = resultType.GetElementType()!;
            isEnumerableResult = true;
        }
        // Check for generic collection types
        else if (resultType.IsGenericType)
        {
            var genericDef = resultType.GetGenericTypeDefinition();
            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IQueryable<>))
            {
                elementType = resultType.GetGenericArguments()[0];
                isEnumerableResult = true;
            }
        }
        // Also check if it implements IEnumerable<T> (but not for primitive types)
        else if (!resultType.IsPrimitive && resultType != typeof(decimal))
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

        // Use the new CypherExpressionBuilder which has complete traversal support
        var (cypher, parameters, context) = CypherExpressionBuilder.BuildGraphQuery(expression, elementType, _provider);

        // Force scalar handling for aggregate queries
        if (context.IsScalarResult)
        {
            isEnumerableResult = false;
            elementType = resultType;
        }

        // Execute the query using the new CypherExpressionBuilder execution method
        var nonNullableParams = parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? (object)DBNull.Value);
        var result = CypherExpressionBuilder.ExecuteQuery(cypher, nonNullableParams, elementType, _provider, context, _transaction);

        // --- UNWRAP HERE IF NEEDED ---
        if (!isEnumerableResult && result is IList listResult && listResult.Count > 0)
        {
            return (TResult)listResult[0]!;
        }

        return HandleResult<TResult>(result, resultType, elementType, isEnumerableResult);
    }

    private TResult HandleResult<TResult>(object? result, Type resultType, Type elementType, bool isEnumerableResult)
    {
        if (result == null)
        {
            return default!;
        }

        // If the result is already the correct type, just return it
        if (result is TResult directResult)
        {
            return directResult;
        }

        // Handle numeric conversions for scalar results (Neo4j sometimes returns long for int, float for double, etc.)
        if (!isEnumerableResult)
        {
            if (resultType == typeof(int) && result is long longValue)
                return (TResult)(object)Convert.ToInt32(longValue);
            if (resultType == typeof(int?) && result is long longValue2)
                return (TResult)(object)Convert.ToInt32(longValue2);
            if (resultType == typeof(double) && result is float floatValue)
                return (TResult)(object)Convert.ToDouble(floatValue);
            if (resultType == typeof(double?) && result is float floatValue2)
                return (TResult)(object)Convert.ToDouble(floatValue2);

            // Try general conversion if types don't match
            if (result.GetType() != resultType)
            {
                try
                {
                    return (TResult)Convert.ChangeType(result, resultType);
                }
                catch
                {
                    // Fallback to direct cast if conversion fails
                    return (TResult)result;
                }
            }
        }

        // For enumerable results, just return as is (should already be correct type)
        return (TResult)result!;
    }

    // Helper to check if a type is a generic list or collection
    private static bool IsGenericListOrCollectionType(Type type)
    {
        if (type.IsArray) return true;
        if (!type.IsGenericType) return false;
        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(List<>) ||
               genericDef == typeof(IList<>) ||
               genericDef == typeof(IEnumerable<>) ||
               genericDef == typeof(ICollection<>);
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

    // Async versions for async LINQ operations
    public async Task<object?> ExecuteAsync(Expression expression)
    {
        var elementType = GetElementTypeFromExpression(expression) ?? _elementType;

        // Use the new CypherExpressionBuilder which has complete traversal support
        var (cypher, parameters, context) = CypherExpressionBuilder.BuildGraphQuery(expression, elementType, _provider);

        // Execute the query using the new CypherExpressionBuilder execution method
        var nonNullableParams = parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? (object)DBNull.Value);
        var result = await CypherExpressionBuilder.ExecuteQueryAsync(cypher, nonNullableParams, elementType, _provider, context, _transaction);

        return result;
    }
}
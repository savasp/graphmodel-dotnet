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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Handlers;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Registry for method handlers that process LINQ method calls.
/// </summary>
internal class MethodHandlerRegistry
{
    private static readonly Lazy<MethodHandlerRegistry> _instance = new(() => new MethodHandlerRegistry());
    private readonly Dictionary<string, IMethodHandler> _handlers;

    public static MethodHandlerRegistry Instance => _instance.Value;

    public bool TryGetHandler(MethodInfo method, out IMethodHandler? handler)
    {
        handler = null;

        // First try exact match with declaring type
        var key = GenerateKey(method.Name, method.DeclaringType);
        if (_handlers.TryGetValue(key, out handler))
        {
            return true;
        }

        // If it's a generic method, try with the generic type definition
        if (method.IsGenericMethod)
        {
            var genericMethod = method.GetGenericMethodDefinition();
            key = GenerateKey(genericMethod.Name, genericMethod.DeclaringType);
            if (_handlers.TryGetValue(key, out handler))
            {
                return true;
            }
        }

        // Try to find by method name in common LINQ types
        var commonTypes = new[] { typeof(Queryable), typeof(Enumerable), typeof(string) };
        foreach (var type in commonTypes)
        {
            key = GenerateKey(method.Name, type);
            if (_handlers.TryGetValue(key, out handler))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSupported(MethodInfo method)
    {
        return TryGetHandler(method, out _);
    }

    public IEnumerable<string> GetRegisteredMethods()
    {
        return _handlers.Keys;
    }

    /// <summary>
    /// Tries to handle the method call expression.
    /// </summary>
    /// <returns>True if the method was handled, false otherwise.</returns>
    public bool TryHandle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;
        var logger = context.LoggerFactory?.CreateLogger(nameof(MethodHandlerRegistry));
        logger?.LogDebug("TryHandle: Looking for handler for method {Method}", methodName);

        if (_handlers.TryGetValue(methodName, out var handler))
        {
            logger?.LogDebug("TryHandle: Found handler for method {Method}: {HandlerType}", methodName, handler.GetType().Name);
            var handled = handler.Handle(context, node, result);
            logger?.LogDebug("TryHandle: Handler for {Method} returned {Handled}", methodName, handled);
            return handled;
        }

        logger?.LogDebug("TryHandle: No handler found for method {Method}", methodName);
        return false;
    }

    private MethodHandlerRegistry()
    {
        _handlers = new Dictionary<string, IMethodHandler>(StringComparer.Ordinal);
        RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        // Create specific handlers for different method categories
        var whereHandler = new WhereMethodHandler();
        var selectHandler = new SelectMethodHandler();
        var selectManyHandler = new SelectManyMethodHandler();
        var orderByHandler = new OrderByMethodHandler();
        var thenByHandler = new ThenByMethodHandler();
        var limitHandler = new LimitMethodHandler();
        var aggregationHandler = new AggregationMethodHandler();
        var graphOperationHandler = new GraphOperationMethodHandler();
        var distinctHandler = new DistinctMethodHandler();
        var groupByHandler = new GroupByMethodHandler();
        var joinHandler = new JoinMethodHandler();
        var unionHandler = new UnionMethodHandler();
        var toListHandler = new ToListMethodHandler();

        // For methods not yet implemented, use the default handler
        var defaultHandler = new DefaultMethodHandler();

        // LINQ standard methods
        RegisterHandler("Where", whereHandler);
        RegisterHandler("Select", selectHandler);
        RegisterHandler("SelectMany", selectManyHandler);
        RegisterHandler("OrderBy", orderByHandler);
        RegisterHandler("OrderByDescending", orderByHandler);
        RegisterHandler("ThenBy", thenByHandler);
        RegisterHandler("ThenByDescending", thenByHandler);
        RegisterHandler("Take", limitHandler);
        RegisterHandler("Skip", limitHandler);
        RegisterHandler("First", aggregationHandler);
        RegisterHandler("FirstOrDefault", aggregationHandler);
        RegisterHandler("Single", aggregationHandler);
        RegisterHandler("SingleOrDefault", aggregationHandler);
        RegisterHandler("Any", aggregationHandler);
        RegisterHandler("All", aggregationHandler);
        RegisterHandler("Count", aggregationHandler);
        RegisterHandler("Distinct", distinctHandler);
        RegisterHandler("GroupBy", groupByHandler);
        RegisterHandler("Join", joinHandler);
        RegisterHandler("Union", unionHandler);
        RegisterHandler("Concat", unionHandler);
        RegisterHandler("ToList", toListHandler);
        RegisterHandler("Include", graphOperationHandler);

        // Graph-specific methods
        RegisterHandler("WithTransaction", graphOperationHandler);
        RegisterHandler("PathSegments", graphOperationHandler);

        // Note: String methods (Contains, StartsWith, etc.), Math methods (Abs, Floor, etc.),
        // and DateTime methods (AddDays, etc.) are handled by their respective expression visitors,
        // not by the handler registry, since they are used within expressions rather than
        // as top-level LINQ query operations.
    }

    private static string GenerateKey(string methodName, Type? declaringType)
    {
        return declaringType != null ? $"{declaringType.FullName}.{methodName}" : methodName;
    }

    /// <summary>
    /// Registers a custom method handler.
    /// </summary>
    private void RegisterHandler(string methodName, IMethodHandler handler)
    {
        _handlers[methodName] = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}

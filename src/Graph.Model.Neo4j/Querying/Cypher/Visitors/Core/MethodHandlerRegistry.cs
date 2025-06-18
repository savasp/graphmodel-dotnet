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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Registry for method handlers that process LINQ method calls.
/// </summary>
internal class MethodHandlerRegistry
{
    private static readonly Lazy<MethodHandlerRegistry> _instance = new(() => new MethodHandlerRegistry());
    private readonly Dictionary<string, IMethodHandler> _handlers;

    public static MethodHandlerRegistry Instance => _instance.Value;

    private MethodHandlerRegistry()
    {
        _handlers = new Dictionary<string, IMethodHandler>(StringComparer.Ordinal);
        RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        // For now, register empty handlers - these will be implemented as needed
        // The actual work is done by the existing visitors
        var emptyHandler = new DefaultMethodHandler();

        // LINQ standard methods
        RegisterHandler("Where", emptyHandler);
        RegisterHandler("Select", emptyHandler);
        RegisterHandler("SelectMany", emptyHandler);
        RegisterHandler("OrderBy", emptyHandler);
        RegisterHandler("OrderByDescending", emptyHandler);
        RegisterHandler("ThenBy", emptyHandler);
        RegisterHandler("ThenByDescending", emptyHandler);
        RegisterHandler("Take", emptyHandler);
        RegisterHandler("Skip", emptyHandler);
        RegisterHandler("First", emptyHandler);
        RegisterHandler("FirstOrDefault", emptyHandler);
        RegisterHandler("Single", emptyHandler);
        RegisterHandler("SingleOrDefault", emptyHandler);
        RegisterHandler("Any", emptyHandler);
        RegisterHandler("All", emptyHandler);
        RegisterHandler("Count", emptyHandler);
        RegisterHandler("Distinct", emptyHandler);
        RegisterHandler("Include", emptyHandler);

        // Graph-specific methods
        RegisterHandler("WithTransaction", emptyHandler);
        RegisterHandler("Traverse", emptyHandler);
        RegisterHandler("Relationships", emptyHandler);
        RegisterHandler("PathSegments", emptyHandler);

        // String methods
        RegisterHandler("StartsWith", emptyHandler);
        RegisterHandler("EndsWith", emptyHandler);
        RegisterHandler("Contains", emptyHandler);
        RegisterHandler("ToLower", emptyHandler);
        RegisterHandler("ToUpper", emptyHandler);
        RegisterHandler("Trim", emptyHandler);
        RegisterHandler("Substring", emptyHandler);

        // Math methods
        RegisterHandler("Abs", emptyHandler);
        RegisterHandler("Floor", emptyHandler);
        RegisterHandler("Ceiling", emptyHandler);
        RegisterHandler("Round", emptyHandler);
        RegisterHandler("Min", emptyHandler);
        RegisterHandler("Max", emptyHandler);

        // DateTime methods
        RegisterHandler("AddDays", emptyHandler);
        RegisterHandler("AddMonths", emptyHandler);
        RegisterHandler("AddYears", emptyHandler);
    }

    private static string GenerateKey(string methodName, Type? declaringType)
    {
        return declaringType != null ? $"{declaringType.FullName}.{methodName}" : methodName;
    }

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
    public bool TryHandle(MethodCallExpression node, Expression result)
    {
        var methodName = node.Method.Name;

        if (_handlers.TryGetValue(methodName, out var handler))
        {
            return handler.Handle(node, result);
        }

        return false;
    }

    /// <summary>
    /// Registers a custom method handler.
    /// </summary>
    public void RegisterHandler(string methodName, IMethodHandler handler)
    {
        _handlers[methodName] = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}

/// <summary>
/// Interface for method handlers.
/// </summary>
internal interface IMethodHandler
{
    /// <summary>
    /// Handles the method call expression.
    /// </summary>
    /// <returns>True if the method was handled, false if it should be passed to the next handler.</returns>
    bool Handle(MethodCallExpression node, Expression result);
}

/// <summary>
/// Base class for method handlers.
/// </summary>
internal abstract class MethodHandlerBase : IMethodHandler
{
    protected readonly CypherQueryScope Scope;
    protected readonly CypherQueryBuilder Builder;
    protected readonly ILogger Logger;

    protected MethodHandlerBase(
        CypherQueryScope scope,
        CypherQueryBuilder builder,
        ILoggerFactory? loggerFactory)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Logger = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;
    }

    public abstract bool Handle(MethodCallExpression node, Expression result);

    protected void ValidateArgumentCount(MethodCallExpression node, int expectedCount)
    {
        if (node.Arguments.Count != expectedCount)
        {
            throw new GraphException(
                $"Method {node.Method.Name} expects {expectedCount} arguments, " +
                $"but received {node.Arguments.Count}");
        }
    }
}

/// <summary>
/// Default method handler that does nothing - placeholder for future implementations.
/// </summary>
internal class DefaultMethodHandler : IMethodHandler
{
    public bool Handle(MethodCallExpression node, Expression result)
    {
        // For now, just return false to indicate the method wasn't handled
        // The actual handling is done by the appropriate visitors
        return false;
    }
}
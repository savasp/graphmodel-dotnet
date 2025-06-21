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
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Registry for method handlers that process LINQ method calls.
/// Uses a multi-tier lookup strategy for reliable handler resolution.
/// </summary>
internal class MethodHandlerRegistry
{
    private static readonly Lazy<MethodHandlerRegistry> _instance = new(() => new MethodHandlerRegistry());

    // Multiple lookup strategies for different scenarios
    private readonly Dictionary<string, IMethodHandler> _exactMatches = new();
    private readonly Dictionary<string, IMethodHandler> _methodNameMatches = new();
    private readonly Dictionary<Type, Dictionary<string, IMethodHandler>> _typeSpecificMatches = new();

    private ILogger? _logger;

    public static MethodHandlerRegistry Instance => _instance.Value;

    public void SetLoggerFactory(ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<MethodHandlerRegistry>() ??
                  NullLogger<MethodHandlerRegistry>.Instance;
    }

    public bool TryGetHandler(MethodInfo method, out IMethodHandler? handler)
    {
        _logger?.LogDebug("Looking for handler: {Method} from {DeclaringType}", method.Name, method.DeclaringType?.Name);

        // Strategy 1: Exact match (most specific)
        var exactKey = GenerateExactKey(method);
        if (_exactMatches.TryGetValue(exactKey, out handler))
        {
            _logger?.LogDebug("Found exact match: {Key}", exactKey);
            return true;
        }

        // Strategy 2: Type-specific match
        if (method.DeclaringType != null &&
            _typeSpecificMatches.TryGetValue(method.DeclaringType, out var typeHandlers) &&
            typeHandlers.TryGetValue(method.Name, out handler))
        {
            _logger?.LogDebug("Found type-specific match: {Type}.{Method}", method.DeclaringType.Name, method.Name);
            return true;
        }

        // Strategy 3: Generic method handling
        if (method.IsGenericMethod)
        {
            var genericMethod = method.GetGenericMethodDefinition();
            var genericKey = GenerateExactKey(genericMethod);
            if (_exactMatches.TryGetValue(genericKey, out handler))
            {
                _logger?.LogDebug("Found generic method match: {Key}", genericKey);
                return true;
            }
        }

        // Strategy 4: Method name fallback (least specific)
        if (_methodNameMatches.TryGetValue(method.Name, out handler))
        {
            _logger?.LogDebug("Found method name fallback: {Method}", method.Name);
            return true;
        }

        _logger?.LogDebug("No handler found for: {Method}", method.Name);
        handler = null;
        return false;
    }

    public bool TryHandle(CypherQueryContext context, MethodCallExpression node, Expression result)
    {
        _logger?.LogDebug("TryHandle called for method: {Method} from {DeclaringType}",
            node.Method.Name, node.Method.DeclaringType?.Name);

        if (TryGetHandler(node.Method, out var handler) && handler != null)
        {
            _logger?.LogDebug("Handling {Method} with {Handler}", node.Method.Name, handler.GetType().Name);
            var handled = handler.Handle(context, node, result);
            _logger?.LogDebug("Handler {Handler} returned {Result}", handler.GetType().Name, handled);
            return handled;
        }

        _logger?.LogDebug("No handler available for {Method}", node.Method.Name);
        return false;
    }

    // Registration methods for different strategies
    public void RegisterExact(MethodInfo method, IMethodHandler handler)
    {
        var key = GenerateExactKey(method);
        _exactMatches[key] = handler;
    }

    public void RegisterForType(Type declaringType, string methodName, IMethodHandler handler)
    {
        if (!_typeSpecificMatches.TryGetValue(declaringType, out var typeHandlers))
        {
            typeHandlers = new Dictionary<string, IMethodHandler>();
            _typeSpecificMatches[declaringType] = typeHandlers;
        }
        typeHandlers[methodName] = handler;
    }

    public void RegisterMethodName(string methodName, IMethodHandler handler)
    {
        _methodNameMatches[methodName] = handler;
    }

    private static string GenerateExactKey(MethodInfo method)
    {
        var declaring = method.DeclaringType?.FullName ?? "Unknown";
        var parameters = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
        return $"{declaring}.{method.Name}({parameters})";
    }

    private MethodHandlerRegistry()
    {
        RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        _logger?.LogDebug("Registering default handlers...");

        // Create handler instances
        var whereHandler = new WhereMethodHandler();
        var selectHandler = new SelectMethodHandler();
        var selectManyHandler = new SelectManyMethodHandler();
        var orderByHandler = new OrderByMethodHandler();
        var thenByHandler = new ThenByMethodHandler();
        var limitHandler = new LimitMethodHandler();
        var aggregationHandler = new AggregationMethodHandler();
        var groupByHandler = new GroupByMethodHandler();
        var joinHandler = new JoinMethodHandler();
        var unionHandler = new UnionMethodHandler();
        var distinctHandler = new DistinctMethodHandler();
        var toListHandler = new ToListMethodHandler();
        var stringMethodHandler = new StringMethodHandler();
        var dateTimeMethodHandler = new DateTimeMethodHandler();
        var mathMethodHandler = new MathMethodHandler();
        var graphOperationHandler = new GraphOperationMethodHandler();
        var defaultHandler = new DefaultMethodHandler();
        var asyncOnlyHandler = new AsyncOnlyMethodHandler(); // For helpful error messages

        // Query building methods (sync is fine - they don't execute)
        RegisterLinqMethod("Where", whereHandler);
        RegisterLinqMethod("Select", selectHandler);
        RegisterLinqMethod("SelectMany", selectManyHandler);
        RegisterLinqMethod("OrderBy", orderByHandler);
        RegisterLinqMethod("OrderByDescending", orderByHandler);
        RegisterLinqMethod("ThenBy", thenByHandler);
        RegisterLinqMethod("ThenByDescending", thenByHandler);
        RegisterLinqMethod("Take", limitHandler);
        RegisterLinqMethod("Skip", limitHandler);
        RegisterLinqMethod("TakeWhile", limitHandler);
        RegisterLinqMethod("SkipWhile", limitHandler);
        RegisterLinqMethod("GroupBy", groupByHandler);
        RegisterLinqMethod("Join", joinHandler);
        RegisterLinqMethod("GroupJoin", joinHandler);
        RegisterLinqMethod("Union", unionHandler);
        RegisterLinqMethod("Intersect", unionHandler);
        RegisterLinqMethod("Except", unionHandler);
        RegisterLinqMethod("Concat", unionHandler);
        RegisterLinqMethod("Distinct", distinctHandler);
        RegisterLinqMethod("DefaultIfEmpty", defaultHandler);
        RegisterLinqMethod("Reverse", defaultHandler);

        // Async-only materialization methods (these execute queries)
        RegisterLinqMethod("ToListAsyncMarker", toListHandler);
        RegisterLinqMethod("ToArrayAsyncMarker", toListHandler);
        RegisterLinqMethod("ToDictionaryAsyncMarker", toListHandler);
        RegisterLinqMethod("ToLookupAsyncMarker", toListHandler);

        // Async-only aggregation methods (these execute queries)
        RegisterLinqMethod("FirstAsyncMarker", aggregationHandler);
        RegisterLinqMethod("FirstOrDefaultAsyncMarker", aggregationHandler);
        RegisterLinqMethod("LastAsyncMarker", aggregationHandler);
        RegisterLinqMethod("LastOrDefaultAsyncMarker", aggregationHandler);
        RegisterLinqMethod("SingleAsyncMarker", aggregationHandler);
        RegisterLinqMethod("SingleOrDefaultAsyncMarker", aggregationHandler);
        RegisterLinqMethod("AnyAsyncMarker", aggregationHandler);
        RegisterLinqMethod("AllAsyncMarker", aggregationHandler);
        RegisterLinqMethod("CountAsyncMarker", aggregationHandler);
        RegisterLinqMethod("LongCountAsyncMarker", aggregationHandler);
        RegisterLinqMethod("SumAsyncMarker", aggregationHandler);
        RegisterLinqMethod("AverageAsyncMarker", aggregationHandler);
        RegisterLinqMethod("MinAsyncMarker", aggregationHandler);
        RegisterLinqMethod("MaxAsyncMarker", aggregationHandler);
        RegisterLinqMethod("ContainsAsyncMarker", aggregationHandler);
        RegisterLinqMethod("ElementAtAsyncMarker", aggregationHandler);
        RegisterLinqMethod("ElementAtOrDefaultAsyncMarker", aggregationHandler);

        // Register sync versions to give helpful error messages
        RegisterLinqMethod("ToList", asyncOnlyHandler);
        RegisterLinqMethod("ToArray", asyncOnlyHandler);
        RegisterLinqMethod("ToDictionary", asyncOnlyHandler);
        RegisterLinqMethod("ToLookup", asyncOnlyHandler);
        RegisterLinqMethod("First", asyncOnlyHandler);
        RegisterLinqMethod("FirstOrDefault", asyncOnlyHandler);
        RegisterLinqMethod("Last", asyncOnlyHandler);
        RegisterLinqMethod("LastOrDefault", asyncOnlyHandler);
        RegisterLinqMethod("Single", asyncOnlyHandler);
        RegisterLinqMethod("SingleOrDefault", asyncOnlyHandler);
        RegisterLinqMethod("Any", asyncOnlyHandler);
        RegisterLinqMethod("All", asyncOnlyHandler);
        RegisterLinqMethod("Count", asyncOnlyHandler);
        RegisterLinqMethod("LongCount", asyncOnlyHandler);
        RegisterLinqMethod("Sum", asyncOnlyHandler);
        RegisterLinqMethod("Average", asyncOnlyHandler);
        RegisterLinqMethod("Min", asyncOnlyHandler);
        RegisterLinqMethod("Max", asyncOnlyHandler);
        RegisterLinqMethod("Contains", asyncOnlyHandler);
        RegisterLinqMethod("ElementAt", asyncOnlyHandler);
        RegisterLinqMethod("ElementAtOrDefault", asyncOnlyHandler);

        // Non-materializing methods stay as-is
        RegisterStringMethods(stringMethodHandler);
        RegisterDateTimeMethods(dateTimeMethodHandler);
        RegisterMathMethods(mathMethodHandler);
        RegisterGraphMethods(graphOperationHandler);

        // Remove the RegisterAsyncVariants() call since we're explicit now

        _logger?.LogDebug("Registered handlers with async-only materialization");
    }

    private void RegisterLinqMethod(string methodName, IMethodHandler handler)
    {
        // Register for common LINQ types
        RegisterForType(typeof(Queryable), methodName, handler);
        RegisterForType(typeof(Enumerable), methodName, handler);

        // Also register as method name fallback
        RegisterMethodName(methodName, handler);
    }

    private void RegisterStringMethods(IMethodHandler handler)
    {
        var stringMethods = new[]
        {
            "Contains", "StartsWith", "EndsWith", "IndexOf", "LastIndexOf",
            "Substring", "ToLower", "ToUpper", "Trim", "TrimStart", "TrimEnd",
            "Replace", "Split", "Join", "IsNullOrEmpty", "IsNullOrWhiteSpace",
            "Length", "PadLeft", "PadRight"
        };

        foreach (var method in stringMethods)
        {
            RegisterForType(typeof(string), method, handler);
            RegisterMethodName(method, handler);
        }
    }

    private void RegisterDateTimeMethods(IMethodHandler handler)
    {
        var dateTimeMethods = new[]
        {
            "AddYears", "AddMonths", "AddDays", "AddHours", "AddMinutes", "AddSeconds",
            "AddMilliseconds", "AddTicks", "Date", "Day", "Month", "Year",
            "Hour", "Minute", "Second", "Millisecond", "DayOfWeek", "DayOfYear",
            "TimeOfDay", "Ticks", "Now", "UtcNow", "Today"
        };

        foreach (var method in dateTimeMethods)
        {
            RegisterForType(typeof(DateTime), method, handler);
            RegisterForType(typeof(DateTimeOffset), method, handler);
            RegisterMethodName(method, handler);
        }
    }

    private void RegisterMathMethods(IMethodHandler handler)
    {
        var mathMethods = new[]
        {
            "Abs", "Acos", "Asin", "Atan", "Atan2", "Ceiling", "Cos", "Cosh",
            "Exp", "Floor", "Log", "Log10", "Max", "Min", "Pow", "Round",
            "Sign", "Sin", "Sinh", "Sqrt", "Tan", "Tanh", "Truncate"
        };

        foreach (var method in mathMethods)
        {
            RegisterForType(typeof(Math), method, handler);
            RegisterMethodName(method, handler);
        }
    }

    private void RegisterGraphMethods(IMethodHandler handler)
    {
        // Core graph traversal methods
        var graphMethods = new[]
        {
            "PathSegments", "Traverse", "WithDepth", "Direction",
            "Include", "ThenInclude", "WithTransaction"
        };

        foreach (var method in graphMethods)
        {
            // Register as method name fallback since we don't know exact declaring types
            RegisterMethodName(method, handler);
        }

        // Also try to register with known graph extension types if we can identify them
        // These would be types like GraphTraversalExtensions, GraphNodeQueryableExtensions, etc.
        var graphExtensionTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => t.Name.Contains("GraphExtensions") ||
                       t.Name.Contains("GraphTraversal") ||
                       t.Name.Contains("GraphNodeQueryable") ||
                       t.Name.Contains("GraphRelationshipQueryable"))
            .ToList();

        foreach (var extensionType in graphExtensionTypes)
        {
            foreach (var method in graphMethods)
            {
                RegisterForType(extensionType, method, handler);
            }
        }
    }

    // Debug helper
    public void LogRegisteredHandlers()
    {
        _logger?.LogInformation("=== Registered Handlers ===");
        _logger?.LogInformation("Exact matches: {Count}", _exactMatches.Count);
        foreach (var kvp in _exactMatches)
        {
            _logger?.LogInformation("  {Key} -> {Handler}", kvp.Key, kvp.Value.GetType().Name);
        }

        _logger?.LogInformation("Type-specific matches: {Count}", _typeSpecificMatches.Count);
        foreach (var typeKvp in _typeSpecificMatches)
        {
            _logger?.LogInformation("  {Type}:", typeKvp.Key.Name);
            foreach (var methodKvp in typeKvp.Value)
            {
                _logger?.LogInformation("    {Method} -> {Handler}", methodKvp.Key, methodKvp.Value.GetType().Name);
            }
        }

        _logger?.LogInformation("Method name fallbacks: {Count}", _methodNameMatches.Count);
        foreach (var kvp in _methodNameMatches)
        {
            _logger?.LogInformation("  {Method} -> {Handler}", kvp.Key, kvp.Value.GetType().Name);
        }
    }

    public bool IsSupported(MethodInfo method) => TryGetHandler(method, out _);

    public IEnumerable<string> GetRegisteredMethods() =>
        _methodNameMatches.Keys
        .Concat(_exactMatches.Keys)
        .Concat(_typeSpecificMatches.SelectMany(t => t.Value.Keys))
        .Distinct();
}

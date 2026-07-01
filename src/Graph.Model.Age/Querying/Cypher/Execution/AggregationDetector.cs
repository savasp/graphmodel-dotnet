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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Detects the type of aggregation operation from an expression tree.
/// Uses <see cref="MethodInfo"/> comparison instead of magic strings
/// for robust operation under obfuscation and AOT compilation.
/// </summary>
internal static class AggregationDetector
{
    // Maps standard LINQ method names to their canonical aggregation type strings.
    // Used during static initialization to build the MethodInfo lookup dictionaries.
    private static readonly Dictionary<string, string> CanonicalNameMap = new()
    {
        [nameof(Enumerable.Count)] = "Count",
        [nameof(Enumerable.LongCount)] = "Count",
        [nameof(Enumerable.Sum)] = "Sum",
        [nameof(Enumerable.Average)] = "Average",
        [nameof(Enumerable.Min)] = "Min",
        [nameof(Enumerable.Max)] = "Max",
        [nameof(Enumerable.Any)] = "Any",
        [nameof(Enumerable.All)] = "All",
        [nameof(Enumerable.First)] = "First",
        [nameof(Enumerable.FirstOrDefault)] = "First",
        [nameof(Enumerable.Last)] = "Last",
        [nameof(Enumerable.LastOrDefault)] = "Last",
        [nameof(Enumerable.Single)] = "Single",
        [nameof(Enumerable.SingleOrDefault)] = "Single",
    };

    /// <summary>
    /// Pre-computed dictionary mapping <see cref="MethodInfo"/> (from <c>Enumerable</c>)
    /// to the canonical aggregation type string. Enables O(1) lookup.
    /// </summary>
    private static readonly Dictionary<MethodInfo, string> EnumerableAggregationMethods;

    /// <summary>
    /// Pre-computed dictionary mapping <see cref="MethodInfo"/> (from <c>Queryable</c>)
    /// to the canonical aggregation type string. Enables O(1) lookup.
    /// </summary>
    private static readonly Dictionary<MethodInfo, string> QueryableAggregationMethods;

    /// <summary>
    /// Combined lookup dictionary of all known standard LINQ aggregation methods.
    /// </summary>
    private static readonly Dictionary<MethodInfo, string> AllAggregationMethods;

    /// <summary>
    /// Fallback map for async and async-marker method names which are custom
    /// project methods (not standard .NET) and may not be discoverable via
    /// <c>typeof(Enumerable)</c> or <c>typeof(Queryable)</c>.
    /// </summary>
    private static readonly Dictionary<string, string> AsyncMarkerMap = new()
    {
        ["CountAsync"] = "Count",
        ["CountAsyncMarker"] = "Count",
        ["LongCountAsync"] = "Count",
        ["LongCountAsyncMarker"] = "Count",
        ["AnyAsync"] = "Any",
        ["AnyAsyncMarker"] = "Any",
        ["AllAsync"] = "All",
        ["AllAsyncMarker"] = "All",
        ["SumAsync"] = "Sum",
        ["SumAsyncMarker"] = "Sum",
        ["AverageAsync"] = "Average",
        ["AverageAsyncMarker"] = "Average",
        ["MinAsync"] = "Min",
        ["MinAsyncMarker"] = "Min",
        ["MaxAsync"] = "Max",
        ["MaxAsyncMarker"] = "Max",
        ["FirstAsync"] = "First",
        ["FirstAsyncMarker"] = "First",
        ["FirstOrDefaultAsync"] = "First",
        ["FirstOrDefaultAsyncMarker"] = "First",
        ["LastAsync"] = "Last",
        ["LastAsyncMarker"] = "Last",
        ["LastOrDefaultAsync"] = "Last",
        ["LastOrDefaultAsyncMarker"] = "Last",
        ["SingleAsync"] = "Single",
        ["SingleAsyncMarker"] = "Single",
        ["SingleOrDefaultAsync"] = "Single",
        ["SingleOrDefaultAsyncMarker"] = "Single",
        ["ToDictionaryAsync"] = "ToDictionary",
        ["ToDictionaryAsyncMarker"] = "ToDictionary",
    };

    static AggregationDetector()
    {
        EnumerableAggregationMethods = BuildMethodMap(typeof(Enumerable));
        QueryableAggregationMethods = BuildMethodMap(typeof(Queryable));

        AllAggregationMethods = new Dictionary<MethodInfo, string>(
            EnumerableAggregationMethods.Count + QueryableAggregationMethods.Count);
        foreach (var kvp in EnumerableAggregationMethods)
            AllAggregationMethods[kvp.Key] = kvp.Value;
        foreach (var kvp in QueryableAggregationMethods)
            AllAggregationMethods.TryAdd(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Builds a dictionary mapping each <see cref="MethodInfo"/> from <paramref name="declaringType"/>
    /// whose name matches a known canonical aggregation name to that canonical name.
    /// For generic methods, stores the generic method definition as the key.
    /// </summary>
    private static Dictionary<MethodInfo, string> BuildMethodMap(Type declaringType)
    {
        var map = new Dictionary<MethodInfo, string>();
        var methods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var method in methods)
        {
            if (CanonicalNameMap.TryGetValue(method.Name, out var canonicalName))
            {
                // For generic methods, use the generic definition as the dictionary key
                // so that any constructed generic instantiation matches.
                var key = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
                map[key] = canonicalName;
            }
        }
        return map;
    }

    /// <summary>
    /// Looks up the <see cref="MethodInfo"/> from the method call to find
    /// the canonical aggregation type name.
    /// </summary>
    private static string? LookupByMethodInfo(MethodInfo method)
    {
        // Normalize: for generic methods, compare against the generic definition
        var key = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;

        if (AllAggregationMethods.TryGetValue(key, out var canonicalName))
            return canonicalName;

        return null;
    }

    public static string? DetectAggregationType(Expression expression)
    {
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            var method = methodCall.Method;

            // O(1) lookup using MethodInfo comparison — robust under obfuscation/AOT
            var result = LookupByMethodInfo(method);
            if (result != null)
                return result;

            // Fallback: async / async-marker variants are custom project methods,
            // not standard .NET Enumerable/Queryable — use string matching since
            // these project-defined names are not subject to obfuscation concerns.
            var methodName = method.Name;
            if (AsyncMarkerMap.TryGetValue(methodName, out var asyncResult))
                return asyncResult;

            if (methodCall.Arguments.Count > 0)
                current = methodCall.Arguments[0];
            else
                break;
        }

        return null;
    }
}

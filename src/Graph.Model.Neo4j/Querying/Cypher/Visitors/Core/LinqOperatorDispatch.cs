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

using System.Reflection;
using Cvoya.Graph.Model;

/// <summary>
/// Identifies which <see cref="CypherQueryVisitor"/> handler a LINQ method call maps to.
/// </summary>
internal enum LinqOperator
{
    Where,
    Select,
    OrderBy,
    OrderByDescending,
    ThenBy,
    ThenByDescending,
    Take,
    Skip,
    Distinct,
    ToListOrArray,
    First,
    Single,
    Last,
    Any,
    All,
    Count,
    Sum,
    Average,
    Min,
    Max,
    Contains,
    ElementAt,
    ElementAtOrDefault,
    SelectMany,
    GroupBy,
    Join,
    Union,
    PathSegments,
    TraversePaths,
    Direction,
    WithDepth,
    Search,
}

/// <summary>
/// Maps LINQ method calls to a <see cref="LinqOperator"/> by <see cref="MethodInfo"/> identity -
/// comparing the call's generic method definition (or the method itself, for non-generic
/// methods) against a table built once via reflection over the known operator-declaring types.
/// This replaces dispatch by matching <c>MethodCallExpression.Method.Name</c> as a string, which
/// cannot distinguish overloads and silently mis-dispatches if an unrelated method happens to
/// share a name.
/// </summary>
internal static class LinqOperatorDispatch
{
    private static readonly Dictionary<MethodInfo, LinqOperator> Table = BuildTable();

    /// <summary>
    /// Resolves the <see cref="LinqOperator"/> for a method call, or <see langword="null"/> if
    /// the method is not a recognized LINQ operator.
    /// </summary>
    public static LinqOperator? Resolve(MethodInfo method)
    {
        var key = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
        return Table.TryGetValue(key, out var op) ? op : null;
    }

    /// <summary>
    /// Every <see cref="MethodInfo"/> registered in the dispatch table, paired with the
    /// <see cref="LinqOperator"/> it resolves to. For completeness tests: enumerating this (rather
    /// than a hand-maintained mirror list) verifies the actual table, so a new registration can't
    /// silently go unverified.
    /// </summary>
    public static IReadOnlyDictionary<MethodInfo, LinqOperator> AllRegisteredMethods => Table;

    private static Dictionary<MethodInfo, LinqOperator> BuildTable()
    {
        var table = new Dictionary<MethodInfo, LinqOperator>();

        void AddAll(Type declaringType, string name, LinqOperator op, int? genericArgCount = null, int? paramCount = null)
        {
            var candidates = declaringType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == name)
                .Where(m => genericArgCount is null || (m.IsGenericMethodDefinition && m.GetGenericArguments().Length == genericArgCount))
                .Where(m => paramCount is null || m.GetParameters().Length == paramCount);

            foreach (var candidate in candidates)
            {
                table[candidate] = op;
            }
        }

        // System.Linq.Queryable standard operators - reached when a chain degrades to plain
        // IQueryable<T> (e.g. after Join, which GraphQueryableExtensions does not shadow).
        AddAll(typeof(Queryable), nameof(Queryable.Where), LinqOperator.Where);
        AddAll(typeof(Queryable), nameof(Queryable.Select), LinqOperator.Select);
        AddAll(typeof(Queryable), nameof(Queryable.OrderBy), LinqOperator.OrderBy);
        AddAll(typeof(Queryable), nameof(Queryable.OrderByDescending), LinqOperator.OrderByDescending);
        AddAll(typeof(Queryable), nameof(Queryable.ThenBy), LinqOperator.ThenBy);
        AddAll(typeof(Queryable), nameof(Queryable.ThenByDescending), LinqOperator.ThenByDescending);
        AddAll(typeof(Queryable), nameof(Queryable.Take), LinqOperator.Take);
        AddAll(typeof(Queryable), nameof(Queryable.Skip), LinqOperator.Skip);
        AddAll(typeof(Queryable), nameof(Queryable.Distinct), LinqOperator.Distinct);
        AddAll(typeof(Queryable), nameof(Queryable.First), LinqOperator.First);
        AddAll(typeof(Queryable), nameof(Queryable.FirstOrDefault), LinqOperator.First);
        AddAll(typeof(Queryable), nameof(Queryable.Single), LinqOperator.Single);
        AddAll(typeof(Queryable), nameof(Queryable.SingleOrDefault), LinqOperator.Single);
        AddAll(typeof(Queryable), nameof(Queryable.Contains), LinqOperator.Contains);
        AddAll(typeof(Queryable), nameof(Queryable.SelectMany), LinqOperator.SelectMany);
        AddAll(typeof(Queryable), nameof(Queryable.GroupBy), LinqOperator.GroupBy);
        AddAll(typeof(Queryable), nameof(Queryable.Join), LinqOperator.Join);
        AddAll(typeof(Queryable), nameof(Queryable.Union), LinqOperator.Union);

        // Cvoya.Graph.Model.GraphQueryableExtensions - the primary IGraphQueryable<T> operator
        // surface. These build their own Expression.Call nodes (not System.Linq.Queryable's), so
        // they need their own dispatch entries distinct from the System.Linq.Queryable ones above.
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Where), LinqOperator.Where);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Select), LinqOperator.Select);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.SelectMany), LinqOperator.SelectMany);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.OrderBy), LinqOperator.OrderBy);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.OrderByDescending), LinqOperator.OrderByDescending);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.ThenBy), LinqOperator.ThenBy);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.ThenByDescending), LinqOperator.ThenByDescending);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Skip), LinqOperator.Skip);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Take), LinqOperator.Take);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Distinct), LinqOperator.Distinct);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.GroupBy), LinqOperator.GroupBy);

        // Cvoya.Graph.Model.QueryTerminals marker methods (internal; reachable via InternalsVisibleTo).
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.ToListAsyncMarker), LinqOperator.ToListOrArray);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.ToArrayAsyncMarker), LinqOperator.ToListOrArray);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.FirstAsyncMarker), LinqOperator.First);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.FirstOrDefaultAsyncMarker), LinqOperator.First);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.SingleAsyncMarker), LinqOperator.Single);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.SingleOrDefaultAsyncMarker), LinqOperator.Single);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.LastAsyncMarker), LinqOperator.Last);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.LastOrDefaultAsyncMarker), LinqOperator.Last);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.AnyAsyncMarker), LinqOperator.Any);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.AllAsyncMarker), LinqOperator.All);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.CountAsyncMarker), LinqOperator.Count);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.LongCountAsyncMarker), LinqOperator.Count);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.SumAsyncMarker), LinqOperator.Sum);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.AverageAsyncMarker), LinqOperator.Average);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.MinAsyncMarker), LinqOperator.Min);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.MaxAsyncMarker), LinqOperator.Max);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.ContainsAsyncMarker), LinqOperator.Contains);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.ElementAtAsyncMarker), LinqOperator.ElementAt);
        AddAll(typeof(QueryTerminals), nameof(QueryTerminals.ElementAtOrDefaultAsyncMarker), LinqOperator.ElementAtOrDefault);

        // QueryableAsyncExtensions.SumAsync/AverageAsync build a marker-based Expression.Call, but
        // are also visited directly in some code paths (e.g. from within Select projections) - map
        // both by name for safety alongside the marker-based mapping above.
        AddAll(typeof(QueryableAsyncExtensions), nameof(QueryableAsyncExtensions.SumAsync), LinqOperator.Sum);
        AddAll(typeof(QueryableAsyncExtensions), nameof(QueryableAsyncExtensions.AverageAsync), LinqOperator.Average);

        // Cvoya.Graph.Model.GraphQueryableExtensions / GraphTraversalExtensions graph-specific operators.
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Search), LinqOperator.Search);
#pragma warning disable CS0618 // Direction/WithDepth are obsolete free-floating modifiers, still dispatched for backward compatibility.
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Direction), LinqOperator.Direction);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.WithDepth), LinqOperator.WithDepth);
#pragma warning restore CS0618
        AddAll(typeof(GraphTraversalExtensions), nameof(GraphTraversalExtensions.PathSegments), LinqOperator.PathSegments);
        AddAll(typeof(GraphTraversalExtensions), nameof(GraphTraversalExtensions.TraversePaths), LinqOperator.TraversePaths);
        // Note: ReverseTraverse is NOT registered here. It is a client-side extension method that
        // eagerly composes PathSegments().Direction(Incoming).Select(ps => ps.EndNode) and calls
        // source.Provider.CreateQuery<T> immediately, rather than deferring - so "ReverseTraverse"
        // never appears as a MethodCallExpression node that reaches this visitor (see the #80
        // characterization finding in TraversalTranslationTests.ReverseTraverse_ProducesPathSegmentsDirectionSelectShape).
        // The dispatch entry is intentionally omitted rather than kept as dead code.

        return table;
    }
}

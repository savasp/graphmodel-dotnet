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

namespace Cvoya.Graph.Model.Querying;

using System.Reflection;

internal static class LinqOperatorDispatch
{
    private static readonly Dictionary<MethodInfo, LinqOperator> Table = BuildTable();

    public static LinqOperator? Resolve(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var key = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
        return Table.TryGetValue(key, out var op) ? op : null;
    }

    public static IReadOnlyDictionary<MethodInfo, LinqOperator> AllRegisteredMethods => Table;

    private static Dictionary<MethodInfo, LinqOperator> BuildTable()
    {
        var table = new Dictionary<MethodInfo, LinqOperator>();

        void AddAll(Type declaringType, string name, LinqOperator op)
        {
            foreach (var candidate in declaringType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                .Where(method => method.Name == name))
            {
                table[candidate] = op;
            }
        }

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

        AddAll(typeof(QueryableAsyncExtensions), nameof(QueryableAsyncExtensions.SumAsync), LinqOperator.Sum);
        AddAll(typeof(QueryableAsyncExtensions), nameof(QueryableAsyncExtensions.AverageAsync), LinqOperator.Average);

        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Search), LinqOperator.Search);
#pragma warning disable CS0618
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.Direction), LinqOperator.Direction);
        AddAll(typeof(GraphQueryableExtensions), nameof(GraphQueryableExtensions.WithDepth), LinqOperator.WithDepth);
#pragma warning restore CS0618
        AddAll(typeof(GraphTraversalExtensions), nameof(GraphTraversalExtensions.PathSegments), LinqOperator.PathSegments);
        AddAll(typeof(GraphTraversalExtensions), nameof(GraphTraversalExtensions.TraversePaths), LinqOperator.TraversePaths);

        return table;
    }
}

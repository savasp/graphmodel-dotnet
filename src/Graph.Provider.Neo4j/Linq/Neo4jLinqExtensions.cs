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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Extension methods for Neo4j LINQ queries.
/// These methods provide additional functionality for executing queries and handling results.
/// </summary>
public static class Neo4jLinqExtensions
{
    /// <summary>
    /// Executes the query and returns the results as a list.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the list.</typeparam>
    /// <param name="source">The source queryable object.</param>
    /// <param name="traversalDepth">The depth of traversal for relationships.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of elements of type <typeparamref name="T"/>.</returns>
    public static async Task<List<T>> ToListAsync<T>(this IQueryable<T> source, int traversalDepth = 1)
    {
        if (source.Provider is Neo4jQueryProvider provider)
        {
            return await provider.ExecuteAsync<List<T>>(
                System.Linq.Expressions.Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.ToList),
                    new[] { typeof(T) },
                    source.Expression
                )
            );
        }

        // Fallback for non-Neo4j providers
        return source.ToList();
    }

    /// <summary>
    /// Executes the query and returns the first element that satisfies a condition.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="source">The source queryable object.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first element of the query.</returns>
    public static async Task<T> FirstAsync<T>(this IQueryable<T> source)
    {
        if (source.Provider is Neo4jQueryProvider provider)
        {
            return await provider.ExecuteAsync<T>(
                System.Linq.Expressions.Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.First),
                    new[] { typeof(T) },
                    source.Expression
                )
            );
        }

        // Fallback for non-Neo4j providers
        return source.First();
    }

    /// <summary>
    /// Executes the query and returns the first element that satisfies a condition, or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="source">The source queryable object.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the first element of the query or default if no element is found.</returns>
    public static async Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source)
    {
        if (source.Provider is Neo4jQueryProvider provider)
        {
            return await provider.ExecuteAsync<T?>(
                System.Linq.Expressions.Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.FirstOrDefault),
                    new[] { typeof(T) },
                    source.Expression
                )
            );
        }

        // Fallback for non-Neo4j providers
        return source.FirstOrDefault();
    }

    /// <summary>
    /// Executes the query and returns the single element that satisfies a condition, or a default value if no such element is found.
    /// </summary>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="source">The source queryable object.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the single element of the query or default if no element is found.</returns>
    public static async Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> source)
    {
        if (source.Provider is Neo4jQueryProvider provider)
        {
            return await provider.ExecuteAsync<T?>(
                System.Linq.Expressions.Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.SingleOrDefault),
                    new[] { typeof(T) },
                    source.Expression
                )
            );
        }

        // Fallback for non-Neo4j providers
        return source.SingleOrDefault();
    }
}

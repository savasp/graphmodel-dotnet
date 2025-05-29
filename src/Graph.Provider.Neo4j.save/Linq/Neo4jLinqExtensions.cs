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
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, int traversalDepth = 1)
    {
        // TODO: Implement async query execution with traversal depth
        return Task.FromResult(source.ToList());
    }
}

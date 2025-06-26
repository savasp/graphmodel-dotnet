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

namespace Cvoya.Graph.Model;

using System.Linq.Expressions;


/// <summary>
/// Represents a graph query provider that handles graph-specific operations
/// </summary>
public interface IGraphQueryProvider : IQueryProvider
{
    /// <summary>
    /// Gets the graph instance associated with this provider
    /// </summary>
    IGraph Graph { get; }

    /// <summary>
    /// Creates a new graph query with the specified expression
    /// </summary>
    /// <typeparam name="T">The type of the elements in the query</typeparam>
    /// <param name="expression">The expression representing the query</param>
    /// <returns>A new graph query with the specified expression</returns>
    new IGraphQueryable<T> CreateQuery<T>(Expression expression);

    /// <summary>
    /// Asynchronously executes the expression and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the expression.</returns>
    Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously executes the expression and returns the result.
    /// </summary>
    /// <param name="expression">The expression to execute.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the expression.</returns>
    Task<object?> ExecuteAsync(Expression expression, CancellationToken cancellationToken = default);
}

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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

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
    /// Creates a new relationship query with the specified expression
    /// </summary>
    /// <param name="expression">The expression representing the query</param>
    /// <returns>A new query with the specified expression</returns>
    IGraphRelationshipQueryable<TRel> CreateRelationshipQuery<TRel>(Expression expression) where TRel : IRelationship;

    /// <summary>
    /// Creates a new node query with the specified expression
    /// </summary>
    /// <param name="expression">The expression representing the query</param>
    /// <returns>A new query with the specified expression</returns>
    IGraphNodeQueryable<TNode> CreateNodeQuery<TNode>(Expression expression) where TNode : INode;

    /// <summary>
    /// Creates a new path segment query for traversing paths in the graph.
    /// </summary>
    /// <typeparam name="TSource">The type of the source node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <typeparam name="TRel">The type of the relationship, which must be an <see cref="IRelationship"/>-derived type.</typeparam>
    /// <typeparam name="TTarget">The type of the target node, which must be an <see cref="INode"/>-derived type.</typeparam>
    /// <param name="expression">The expression representing the path segment query.</param>
    /// <returns>An <see cref="IGraphQueryable{T}"/> where T is <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/> for querying path segments.</returns>
    IGraphQueryable<IGraphPathSegment<TSource, TRel, TTarget>> CreatePathSegmentQuery<TSource, TRel, TTarget>(Expression expression)
        where TSource : INode
        where TRel : IRelationship
        where TTarget : INode;

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

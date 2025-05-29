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
/// Fluent interface for building complex graph queries
/// </summary>
/// <typeparam name="T">The type of the starting entity</typeparam>
public interface IGraphQueryBuilder<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Applies a filter to the current query
    /// </summary>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>A query builder with the filter applied</returns>
    IGraphQueryBuilder<T> Where(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Traverses to connected nodes via a relationship
    /// </summary>
    /// <typeparam name="TRel">The type of relationship to traverse</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A query builder for the target nodes</returns>
    IGraphQueryBuilder<TTarget> TraverseTo<TRel, TTarget>()
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new();

    /// <summary>
    /// Traverses to connected nodes via a relationship with filtering
    /// </summary>
    /// <typeparam name="TRel">The type of relationship to traverse</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="relationshipPredicate">Filter for the relationship</param>
    /// <param name="targetPredicate">Filter for the target nodes</param>
    /// <returns>A query builder for the filtered target nodes</returns>
    IGraphQueryBuilder<TTarget> TraverseTo<TRel, TTarget>(
        Expression<Func<TRel, bool>>? relationshipPredicate = null,
        Expression<Func<TTarget, bool>>? targetPredicate = null)
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new();

    /// <summary>
    /// Traverses via a specific relationship type
    /// </summary>
    /// <typeparam name="TNode">The type of source node (must be same as T)</typeparam>
    /// <typeparam name="TRel">The type of relationship to traverse</typeparam>
    /// <returns>A relationship traversal builder</returns>
    IRelationshipTraversalBuilder<TNode, TRel> Via<TNode, TRel>()
        where TNode : class, INode, new()
        where TRel : class, IRelationship, new();

    /// <summary>
    /// Follows a path of specified length
    /// </summary>
    /// <typeparam name="TNode">The type of source node (must be same as T)</typeparam>
    /// <param name="minLength">The minimum path length</param>
    /// <param name="maxLength">The maximum path length</param>
    /// <returns>A path traversal builder</returns>
    IPathTraversalBuilder<TNode> FollowPath<TNode>(int minLength, int maxLength)
        where TNode : class, INode, new();

    /// <summary>
    /// Finds the shortest path to target nodes
    /// </summary>
    /// <typeparam name="TNode">The type of source node (must be same as T)</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A shortest path builder</returns>
    IShortestPathBuilder<TNode, TTarget> ShortestPathTo<TNode, TTarget>()
        where TNode : class, INode, new()
        where TTarget : class, INode, new();

    /// <summary>
    /// Aggregates connected data
    /// </summary>
    /// <typeparam name="TResult">The type of aggregation result</typeparam>
    /// <param name="aggregator">The aggregation expression</param>
    /// <returns>A queryable for the aggregated results</returns>
    IGraphQueryable<TResult> Aggregate<TResult>(Expression<Func<IGrouping<T, IEntity>, TResult>> aggregator) where TResult : class;

    /// <summary>
    /// Groups entities by a key
    /// </summary>
    /// <typeparam name="TKey">The type of the grouping key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>A group traversal builder</returns>
    IGroupTraversalBuilder<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Projects the current entities to a different type
    /// </summary>
    /// <typeparam name="TResult">The type to project to</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the projected results</returns>
    IGraphQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) where TResult : class;

    /// <summary>
    /// Projects the current entities to multiple results (flattening)
    /// </summary>
    /// <typeparam name="TResult">The type of the flattened results</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the flattened results</returns>
    IGraphQueryable<TResult> SelectMany<TResult>(Expression<Func<T, IEnumerable<TResult>>> selector) where TResult : class;

    /// <summary>
    /// Orders the results by a key
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder</returns>
    IOrderedGraphQueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Orders the results by a key in descending order
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder</returns>
    IOrderedGraphQueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Limits the number of results
    /// </summary>
    /// <param name="count">The maximum number of results</param>
    /// <returns>A query builder with the limit applied</returns>
    IGraphQueryBuilder<T> Take(int count);

    /// <summary>
    /// Skips a number of results
    /// </summary>
    /// <param name="count">The number of results to skip</param>
    /// <returns>A query builder with the skip applied</returns>
    IGraphQueryBuilder<T> Skip(int count);

    /// <summary>
    /// Applies distinct filtering to the results
    /// </summary>
    /// <returns>A query builder with distinct applied</returns>
    IGraphQueryBuilder<T> Distinct();

    /// <summary>
    /// Applies distinct filtering using a custom comparer
    /// </summary>
    /// <param name="comparer">The comparer to use for distinctness</param>
    /// <returns>A query builder with distinct applied</returns>
    IGraphQueryBuilder<T> Distinct(IEqualityComparer<T> comparer);

    /// <summary>
    /// Unions the current query with another query
    /// </summary>
    /// <param name="other">The other query to union with</param>
    /// <returns>A query builder representing the union</returns>
    IGraphQueryBuilder<T> Union(IGraphQueryable<T> other);

    /// <summary>
    /// Intersects the current query with another query
    /// </summary>
    /// <param name="other">The other query to intersect with</param>
    /// <returns>A query builder representing the intersection</returns>
    IGraphQueryBuilder<T> Intersect(IGraphQueryable<T> other);

    /// <summary>
    /// Excludes results that match another query
    /// </summary>
    /// <param name="other">The query whose results to exclude</param>
    /// <returns>A query builder with the exclusion applied</returns>
    IGraphQueryBuilder<T> Except(IGraphQueryable<T> other);

    /// <summary>
    /// Sets traversal depth constraints
    /// </summary>
    /// <param name="minDepth">The minimum traversal depth</param>
    /// <param name="maxDepth">The maximum traversal depth</param>
    /// <returns>A query builder with the depth constraints applied</returns>
    IGraphQueryBuilder<T> WithDepth(int minDepth, int maxDepth);

    /// <summary>
    /// Sets maximum traversal depth
    /// </summary>
    /// <param name="maxDepth">The maximum traversal depth</param>
    /// <returns>A query builder with the depth constraint applied</returns>
    IGraphQueryBuilder<T> WithDepth(int maxDepth);

    /// <summary>
    /// Applies graph operation options
    /// </summary>
    /// <param name="options">The options to apply</param>
    /// <returns>A query builder with the options applied</returns>
    IGraphQueryBuilder<T> WithOptions(GraphOperationOptions options);

    /// <summary>
    /// Executes within a specific transaction
    /// </summary>
    /// <param name="transaction">The transaction to use</param>
    /// <returns>A query builder bound to the transaction</returns>
    IGraphQueryBuilder<T> InTransaction(IGraphTransaction transaction);

    /// <summary>
    /// Converts the query builder to a queryable for final execution
    /// </summary>
    /// <returns>A queryable for the built query</returns>
    IGraphQueryable<T> AsQueryable();

    /// <summary>
    /// Executes the query and returns the results
    /// </summary>
    /// <returns>The query results</returns>
    Task<List<T>> ToListAsync();

    /// <summary>
    /// Executes the query and returns the first result
    /// </summary>
    /// <returns>The first result</returns>
    Task<T> FirstAsync();

    /// <summary>
    /// Executes the query and returns the first result or default
    /// </summary>
    /// <returns>The first result or default value</returns>
    Task<T?> FirstOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns a single result
    /// </summary>
    /// <returns>The single result</returns>
    Task<T> SingleAsync();

    /// <summary>
    /// Executes the query and returns a single result or default
    /// </summary>
    /// <returns>The single result or default value</returns>
    Task<T?> SingleOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the count of results
    /// </summary>
    /// <returns>The count of results</returns>
    Task<int> CountAsync();

    /// <summary>
    /// Executes the query and checks if any results exist
    /// </summary>
    /// <returns>True if any results exist, false otherwise</returns>
    Task<bool> AnyAsync();

    /// <summary>
    /// Executes the query and checks if any results exist matching a predicate
    /// </summary>
    /// <param name="predicate">The predicate to check</param>
    /// <returns>True if any results match the predicate, false otherwise</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
}

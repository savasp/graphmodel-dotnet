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
    /// <typeparam name="TRel">The type of relationship to traverse</typeparam>
    /// <returns>A relationship traversal builder</returns>
    IRelationshipTraversalBuilder<T, TRel> Via<TRel>() where TRel : class, IRelationship, new();

    /// <summary>
    /// Follows a path of specified length
    /// </summary>
    /// <param name="minLength">The minimum path length</param>
    /// <param name="maxLength">The maximum path length</param>
    /// <returns>A path traversal builder</returns>
    IPathTraversalBuilder<T> FollowPath(int minLength, int maxLength);

    /// <summary>
    /// Finds the shortest path to target nodes
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A shortest path builder</returns>
    IShortestPathBuilder<T, TTarget> ShortestPathTo<TTarget>() where TTarget : class, INode, new();

    /// <summary>
    /// Aggregates connected data
    /// </summary>
    /// <typeparam name="TResult">The type of aggregation result</typeparam>
    /// <param name="aggregator">The aggregation expression</param>
    /// <returns>A queryable for the aggregated results</returns>
    IGraphQueryable<TResult> Aggregate<TResult>(Expression<Func<IGrouping<T, IEntity>, TResult>> aggregator);

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
    IGraphQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// Projects the current entities to multiple results (flattening)
    /// </summary>
    /// <typeparam name="TResult">The type of the flattened results</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the flattened results</returns>
    IGraphQueryable<TResult> SelectMany<TResult>(Expression<Func<T, IEnumerable<TResult>>> selector);

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

/// <summary>
/// Ordered query builder interface
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
public interface IOrderedGraphQueryBuilder<T> : IGraphQueryBuilder<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Applies a secondary ordering
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder with the secondary ordering</returns>
    IOrderedGraphQueryBuilder<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Applies a secondary ordering in descending order
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder with the secondary descending ordering</returns>
    IOrderedGraphQueryBuilder<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
}

/// <summary>
/// Builder for relationship traversal operations
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TRel">The type of the relationship</typeparam>
public interface IRelationshipTraversalBuilder<TSource, TRel>
    where TSource : class, INode, new()
    where TRel : class, IRelationship, new()
{
    /// <summary>
    /// Filters the relationships being traversed
    /// </summary>
    /// <param name="predicate">The relationship filter</param>
    /// <returns>A relationship traversal builder with the filter applied</returns>
    IRelationshipTraversalBuilder<TSource, TRel> Where(Expression<Func<TRel, bool>> predicate);

    /// <summary>
    /// Specifies the direction of traversal
    /// </summary>
    /// <param name="direction">The traversal direction</param>
    /// <returns>A relationship traversal builder with the direction specified</returns>
    IRelationshipTraversalBuilder<TSource, TRel> InDirection(TraversalDirection direction);

    /// <summary>
    /// Traverses to the target nodes
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A query builder for the target nodes</returns>
    IGraphQueryBuilder<TTarget> To<TTarget>() where TTarget : class, INode, new();

    /// <summary>
    /// Traverses to the target nodes with filtering
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="predicate">The target node filter</param>
    /// <returns>A query builder for the filtered target nodes</returns>
    IGraphQueryBuilder<TTarget> To<TTarget>(Expression<Func<TTarget, bool>> predicate) 
        where TTarget : class, INode, new();

    /// <summary>
    /// Returns the relationships themselves
    /// </summary>
    /// <returns>A query builder for the relationships</returns>
    IGraphQueryBuilder<TRel> Relationships();

    /// <summary>
    /// Returns the complete paths including source, relationship, and target
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A query builder for the paths</returns>
    IQueryable<IGraphPath<TSource, TRel, TTarget>> Paths<TTarget>() 
        where TTarget : class, INode, new();
}

/// <summary>
/// Builder for path traversal operations
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
public interface IPathTraversalBuilder<TSource> where TSource : class, INode, new()
{
    /// <summary>
    /// Specifies which relationship types to include in the path
    /// </summary>
    /// <param name="relationshipTypes">The relationship types to include</param>
    /// <returns>A path traversal builder with the relationship types specified</returns>
    IPathTraversalBuilder<TSource> ViaRelationships(params Type[] relationshipTypes);

    /// <summary>
    /// Specifies which relationship types to include in the path using generic parameters
    /// </summary>
    /// <typeparam name="TRel1">The first relationship type</typeparam>
    /// <returns>A path traversal builder with the relationship type specified</returns>
    IPathTraversalBuilder<TSource> ViaRelationships<TRel1>() where TRel1 : class, IRelationship, new();

    /// <summary>
    /// Specifies which relationship types to include in the path using multiple generic parameters
    /// </summary>
    /// <typeparam name="TRel1">The first relationship type</typeparam>
    /// <typeparam name="TRel2">The second relationship type</typeparam>
    /// <returns>A path traversal builder with the relationship types specified</returns>
    IPathTraversalBuilder<TSource> ViaRelationships<TRel1, TRel2>() 
        where TRel1 : class, IRelationship, new()
        where TRel2 : class, IRelationship, new();

    /// <summary>
    /// Specifies the direction for path traversal
    /// </summary>
    /// <param name="direction">The traversal direction</param>
    /// <returns>A path traversal builder with the direction specified</returns>
    IPathTraversalBuilder<TSource> InDirection(TraversalDirection direction);

    /// <summary>
    /// Traverses to target nodes
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A query builder for the target nodes</returns>
    IGraphQueryBuilder<TTarget> To<TTarget>() where TTarget : class, INode, new();

    /// <summary>
    /// Traverses to target nodes with filtering
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="predicate">The target node filter</param>
    /// <returns>A query builder for the filtered target nodes</returns>
    IGraphQueryBuilder<TTarget> To<TTarget>(Expression<Func<TTarget, bool>> predicate) 
        where TTarget : class, INode, new();

    /// <summary>
    /// Returns the complete paths
    /// </summary>
    /// <returns>A query builder for the paths</returns>
    IQueryable<IGraphMultiPath> Paths();
}

/// <summary>
/// Builder for shortest path operations
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TTarget">The type of the target entity</typeparam>
public interface IShortestPathBuilder<TSource, TTarget>
    where TSource : class, INode, new()
    where TTarget : class, INode, new()
{
    /// <summary>
    /// Specifies which relationship types to consider for the shortest path
    /// </summary>
    /// <param name="relationshipTypes">The relationship types to consider</param>
    /// <returns>A shortest path builder with the relationship types specified</returns>
    IShortestPathBuilder<TSource, TTarget> ViaRelationships(params Type[] relationshipTypes);

    /// <summary>
    /// Specifies the maximum length for the shortest path
    /// </summary>
    /// <param name="maxLength">The maximum path length</param>
    /// <returns>A shortest path builder with the maximum length specified</returns>
    IShortestPathBuilder<TSource, TTarget> WithMaxLength(int maxLength);

    /// <summary>
    /// Specifies the property to use for edge weights
    /// </summary>
    /// <param name="weightProperty">The name of the weight property</param>
    /// <returns>A shortest path builder with weighted path calculation</returns>
    IShortestPathBuilder<TSource, TTarget> WithWeights(string weightProperty);

    /// <summary>
    /// Specifies a custom weight calculation
    /// </summary>
    /// <param name="weightCalculator">The weight calculation expression</param>
    /// <returns>A shortest path builder with custom weight calculation</returns>
    IShortestPathBuilder<TSource, TTarget> WithWeights(Expression<Func<IRelationship, double>> weightCalculator);

    /// <summary>
    /// Filters target nodes
    /// </summary>
    /// <param name="predicate">The target node filter</param>
    /// <returns>A shortest path builder with target filtering</returns>
    IShortestPathBuilder<TSource, TTarget> Where(Expression<Func<TTarget, bool>> predicate);

    /// <summary>
    /// Returns the target nodes found via shortest path
    /// </summary>
    /// <returns>A query builder for the target nodes</returns>
    IGraphQueryBuilder<TTarget> Nodes();

    /// <summary>
    /// Returns the complete shortest paths
    /// </summary>
    /// <returns>A query builder for the shortest paths</returns>
    IQueryable<IGraphMultiPath> Paths();
}

/// <summary>
/// Builder for group traversal operations
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TKey">The type of the grouping key</typeparam>
public interface IGroupTraversalBuilder<TSource, TKey> where TSource : class, IEntity, new()
{
    /// <summary>
    /// Applies a filter to the grouped results
    /// </summary>
    /// <param name="predicate">The group filter</param>
    /// <returns>A group traversal builder with the filter applied</returns>
    IGroupTraversalBuilder<TSource, TKey> Having(Expression<Func<IGrouping<TKey, TSource>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results
    /// </summary>
    /// <typeparam name="TResult">The type of the projection result</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the projected group results</returns>
    IGraphQueryable<TResult> Select<TResult>(Expression<Func<IGrouping<TKey, TSource>, TResult>> selector);

    /// <summary>
    /// Orders the groups by a key
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> OrderBy<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);

    /// <summary>
    /// Orders the groups by a key in descending order
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> OrderByDescending<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);
}

/// <summary>
/// Ordered group traversal builder interface
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TKey">The type of the grouping key</typeparam>
public interface IOrderedGroupTraversalBuilder<TSource, TKey> : IGroupTraversalBuilder<TSource, TKey> 
    where TSource : class, IEntity, new()
{
    /// <summary>
    /// Applies a secondary ordering to the groups
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder with secondary ordering</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> ThenBy<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);

    /// <summary>
    /// Applies a secondary ordering to the groups in descending order
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder with secondary descending ordering</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> ThenByDescending<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);
}
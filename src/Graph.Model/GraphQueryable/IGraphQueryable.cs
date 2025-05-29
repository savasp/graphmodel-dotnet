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

/// <summary>
/// Enhanced queryable interface for graph operations that extends IQueryable&lt;T&gt; 
/// with graph-specific functionality while maintaining full LINQ compatibility.
/// </summary>
/// <typeparam name="T">The type of elements in the query</typeparam>
public interface IGraphQueryable<T> : IQueryable<T> where T : class
{
    /// <summary>
    /// Gets the transaction context for this query, if any
    /// </summary>
    IGraphTransaction? Transaction { get; }

    /// <summary>
    /// Gets metadata about the query execution context
    /// </summary>
    IGraphQueryContext Context { get; }

    /// <summary>
    /// Applies a specific traversal depth to this query
    /// </summary>
    /// <param name="depth">The maximum traversal depth</param>
    /// <returns>A new query with the specified depth</returns>
    IGraphQueryable<T> WithDepth(int depth);

    /// <summary>
    /// Applies a range of traversal depths to this query
    /// </summary>
    /// <param name="minDepth">The minimum traversal depth</param>
    /// <param name="maxDepth">The maximum traversal depth</param>
    /// <returns>A new query with the specified depth range</returns>
    IGraphQueryable<T> WithDepth(int minDepth, int maxDepth);

    /// <summary>
    /// Executes this query within a specific transaction context
    /// </summary>
    /// <param name="transaction">The transaction to use</param>
    /// <returns>A new query bound to the specified transaction</returns>
    IGraphQueryable<T> InTransaction(IGraphTransaction transaction);

    /// <summary>
    /// Provides query optimization hints to the underlying provider
    /// </summary>
    /// <param name="hint">The optimization hint</param>
    /// <returns>A new query with the specified hint</returns>
    IGraphQueryable<T> WithHint(string hint);

    /// <summary>
    /// Provides multiple query optimization hints to the underlying provider
    /// </summary>
    /// <param name="hints">The optimization hints</param>
    /// <returns>A new query with the specified hints</returns>
    IGraphQueryable<T> WithHints(params string[] hints);

    /// <summary>
    /// Suggests using a specific index for this query
    /// </summary>
    /// <param name="indexName">The name of the index to use</param>
    /// <returns>A new query with the index hint</returns>
    IGraphQueryable<T> UseIndex(string indexName);

    /// <summary>
    /// Initiates a graph traversal from this query
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TRel">The type of relationship to traverse</typeparam>
    /// <returns>A traversal builder for the specified relationship type</returns>
    IGraphTraversal<TSource, TRel> Traverse<TSource, TRel>()
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new();

    /// <summary>
    /// Initiates graph pattern matching from this query
    /// </summary>
    /// <param name="pattern">The pattern to match</param>
    /// <returns>A pattern matcher for complex graph patterns</returns>
    IGraphPattern<TEntity> Match<TEntity>(string pattern) where TEntity : class, IEntity, new();

    /// <summary>
    /// Creates a graph query builder for complex multi-step queries
    /// </summary>
    /// <returns>A fluent query builder</returns>
    IGraphQueryBuilder<TEntity> Query<TEntity>() where TEntity : class, IEntity, new();

    /// <summary>
    /// Enables query result caching for this query
    /// </summary>
    /// <param name="duration">How long to cache results</param>
    /// <returns>A new query with caching enabled</returns>
    IGraphQueryable<T> Cached(TimeSpan duration);

    /// <summary>
    /// Enables query result caching with a custom cache key
    /// </summary>
    /// <param name="cacheKey">The cache key to use</param>
    /// <param name="duration">How long to cache results</param>
    /// <returns>A new query with caching enabled</returns>
    IGraphQueryable<T> Cached(string cacheKey, TimeSpan duration);

    /// <summary>
    /// Includes additional graph metadata in the query results
    /// </summary>
    /// <param name="metadata">The types of metadata to include</param>
    /// <returns>A new query that includes the specified metadata</returns>
    IGraphQueryable<T> IncludeMetadata(GraphMetadataTypes metadata);

    /// <summary>
    /// Sets query execution timeout
    /// </summary>
    /// <param name="timeout">The maximum execution time</param>
    /// <returns>A new query with the specified timeout</returns>
    IGraphQueryable<T> WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Enables profiling and performance monitoring for this query
    /// </summary>
    /// <returns>A new query with profiling enabled</returns>
    IGraphQueryable<T> WithProfiling();

    /// <summary>
    /// Enables cascade delete behavior for this query
    /// </summary>
    /// <returns>A new query with cascade delete enabled</returns>
    IGraphQueryable<T> WithCascadeDelete();
}



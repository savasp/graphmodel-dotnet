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
/// Extension methods for enhancing IQueryable&lt;T&gt; with graph-specific operations
/// </summary>
public static class GraphQueryableExtensions
{
    /// <summary>
    /// Converts an IQueryable&lt;T&gt; to an IGraphQueryable&lt;T&gt; with default options
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>An enhanced graph queryable</returns>
    public static IGraphQueryable<T> AsGraphQueryable<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        // Implementation would be provider-specific
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }

    /// <summary>
    /// Converts an IQueryable&lt;T&gt; to an IGraphQueryable&lt;T&gt; with specific options
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="options">The graph operation options</param>
    /// <returns>An enhanced graph queryable</returns>
    public static IGraphQueryable<T> AsGraphQueryable<T>(this IQueryable<T> source, GraphOperationOptions options)
        where T : class, IEntity, new()
    {
        // Implementation would be provider-specific
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }

    /// <summary>
    /// Initiates a fluent graph query from a queryable
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A graph query builder</returns>
    public static IGraphQueryBuilder<T> Query<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        // Implementation would be provider-specific
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }

    /// <summary>
    /// Applies graph operation options to a queryable
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="options">The options to apply</param>
    /// <returns>A queryable with the options applied</returns>
    public static IGraphQueryable<T> WithOptions<T>(this IQueryable<T> source, GraphOperationOptions options)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().WithOptions(options);
    }

    /// <summary>
    /// Applies a traversal depth to a queryable
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="depth">The traversal depth</param>
    /// <returns>A queryable with the depth applied</returns>
    public static IGraphQueryable<T> WithDepth<T>(this IQueryable<T> source, int depth)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().WithDepth(depth);
    }

    /// <summary>
    /// Executes a queryable within a transaction
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="transaction">The transaction to use</param>
    /// <returns>A queryable bound to the transaction</returns>
    public static IGraphQueryable<T> InTransaction<T>(this IQueryable<T> source, IGraphTransaction transaction)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().InTransaction(transaction);
    }
}

/// <summary>
/// Extension methods for graph traversal operations
/// </summary>
public static class GraphTraversalExtensions
{
    /// <summary>
    /// Traverses from nodes to connected nodes via a specific relationship type
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for the connected target nodes</returns>
    public static IGraphQueryable<TTarget> ConnectedTo<TSource, TRel, TTarget>(
        this IQueryable<TSource> source)
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new()
    {
        return source.AsGraphQueryable().Traverse<TSource, TRel>().To<TTarget>();
    }

    /// <summary>
    /// Traverses from nodes to connected nodes via a specific relationship type with filtering
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="relationshipPredicate">Filter for relationships</param>
    /// <param name="targetPredicate">Filter for target nodes</param>
    /// <returns>A queryable for the filtered connected target nodes</returns>
    public static IGraphQueryable<TTarget> ConnectedTo<TSource, TRel, TTarget>(
        this IQueryable<TSource> source,
        Expression<Func<TRel, bool>>? relationshipPredicate = null,
        Expression<Func<TTarget, bool>>? targetPredicate = null)
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new()
    {
        var traversal = source.AsGraphQueryable().Traverse<TSource, TRel>();

        if (relationshipPredicate != null)
            traversal = traversal.WhereRelationship(relationshipPredicate);

        if (targetPredicate != null)
            return traversal.To<TTarget>(targetPredicate);
        else
            return traversal.To<TTarget>();
    }

    /// <summary>
    /// Finds nodes connected by incoming relationships of a specific type
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for nodes connected by incoming relationships</returns>
    public static IGraphQueryable<TTarget> ConnectedBy<TSource, TRel, TTarget>(
        this IQueryable<TSource> source)
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new()
    {
        return source.AsGraphQueryable().Traverse<TSource, TRel>().InDirection(TraversalDirection.Incoming).To<TTarget>();
    }

    /// <summary>
    /// Finds the shortest path between source nodes and target nodes
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="maxLength">Maximum path length to consider</param>
    /// <returns>A queryable for shortest path results</returns>
    public static IQueryable<IGraphMultiPath> ShortestPathTo<TSource, TTarget>(
        this IQueryable<TSource> source,
        int maxLength = 10)
        where TSource : class, INode, new()
        where TTarget : class, INode, new()
    {
        return source.AsGraphQueryable().Query<TSource>().ShortestPathTo<TSource, TTarget>().WithMaxLength(maxLength).Paths();
    }

    /// <summary>
    /// Finds all paths between source nodes and target nodes within a length limit
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="minLength">Minimum path length</param>
    /// <param name="maxLength">Maximum path length</param>
    /// <returns>A queryable for all paths within the length constraints</returns>
    public static IQueryable<IGraphMultiPath> AllPathsTo<TSource, TTarget>(
        this IQueryable<TSource> source,
        int minLength = 1,
        int maxLength = 5)
        where TSource : class, INode, new()
        where TTarget : class, INode, new()
    {
        return source.AsGraphQueryable().Query<TSource>().FollowPath<TSource>(minLength, maxLength).Paths();
    }

    /// <summary>
    /// Gets all relationships of a specific type from the source nodes
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TRel">The type of relationships</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="direction">The direction to traverse</param>
    /// <returns>A queryable for the relationships</returns>
    public static IGraphQueryable<TRel> Relationships<TSource, TRel>(
        this IQueryable<TSource> source,
        TraversalDirection direction = TraversalDirection.Outgoing)
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
    {
        return source.AsGraphQueryable().Traverse<TSource, TRel>().InDirection(direction).Relationships();
    }

    /// <summary>
    /// Gets neighbors of the source nodes (nodes connected by any relationship)
    /// </summary>
    /// <typeparam name="TSource">The type of source nodes</typeparam>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="direction">The direction to traverse</param>
    /// <returns>A queryable for neighboring nodes</returns>
    public static IGraphQueryable<TTarget> Neighbors<TSource, TTarget>(
        this IQueryable<TSource> source,
        TraversalDirection direction = TraversalDirection.Both)
        where TSource : class, INode, new()
        where TTarget : class, INode, new()
    {
        return source.AsGraphQueryable().Query<TSource>().FollowPath<TSource>(1, 1).InDirection(direction).To<TTarget>();
    }
}

/// <summary>
/// Extension methods for graph pattern matching operations
/// </summary>
public static class GraphPatternExtensions
{
    /// <summary>
    /// Initiates pattern matching from a queryable
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="pattern">The pattern string to match</param>
    /// <returns>A pattern matcher</returns>
    public static IGraphPattern<T> Match<T>(this IQueryable<T> source, string pattern)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Match(pattern);
    }

    /// <summary>
    /// Finds triangles (3-cycles) in the graph starting from the source nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for triangle patterns</returns>
    public static IGraphQueryable<IPatternMatch> Triangles<T>(this IQueryable<T> source)
        where T : class, INode, new()
    {
        return source.AsGraphQueryable()
            .Match("(a)-[]->(b)-[]->(c)-[]->(a)")
            .Matches();
    }

    /// <summary>
    /// Finds squares (4-cycles) in the graph starting from the source nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for square patterns</returns>
    public static IGraphQueryable<IPatternMatch> Squares<T>(this IQueryable<T> source)
        where T : class, INode, new()
    {
        return source.AsGraphQueryable()
            .Match("(a)-[]->(b)-[]->(c)-[]->(d)-[]->(a)")
            .Matches();
    }

    /// <summary>
    /// Finds star patterns (hub nodes with multiple connections) centered on the source nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="minConnections">Minimum number of connections to be considered a star</param>
    /// <returns>A queryable for star pattern results</returns>
    public static IGraphQueryable<IPatternMatch> Stars<T>(this IQueryable<T> source, int minConnections = 3)
        where T : class, INode, new()
    {
        // This would require a more complex pattern or aggregation
        // For simplicity, using a basic pattern here
        return source.AsGraphQueryable()
            .Match($"(center)-[]->(leaf)")
            .Matches();
    }
}

/// <summary>
/// Extension methods for graph analysis operations
/// </summary>
public static class GraphAnalysisExtensions
{
    /// <summary>
    /// Calculates degree centrality for nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for degree centrality results</returns>
    public static IGraphQueryable<INodeCentrality<T>> DegreeCentrality<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Analysis().Centrality(CentralityType.Degree);
    }

    /// <summary>
    /// Calculates betweenness centrality for nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for betweenness centrality results</returns>
    public static IGraphQueryable<INodeCentrality<T>> BetweennessCentrality<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Analysis().Centrality(CentralityType.Betweenness);
    }

    /// <summary>
    /// Calculates PageRank for nodes
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for PageRank results</returns>
    public static IGraphQueryable<INodeCentrality<T>> PageRank<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Analysis().Centrality(CentralityType.PageRank);
    }

    /// <summary>
    /// Detects communities using the Louvain algorithm
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for community detection results</returns>
    public static IGraphQueryable<ICommunity<T>> Communities<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Analysis().Communities(CommunityDetectionAlgorithm.Louvain);
    }

    /// <summary>
    /// Finds strongly connected components
    /// </summary>
    /// <typeparam name="T">The type of nodes</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable for strongly connected components</returns>
    public static IGraphQueryable<IConnectedComponent<T>> StronglyConnectedComponents<T>(this IQueryable<T> source)
        where T : class, IEntity, new()
    {
        return source.AsGraphQueryable().Analysis().StronglyConnectedComponents();
    }

    /// <summary>
    /// Extension method to access graph analysis functionality
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A graph analysis interface</returns>
    public static IGraphAnalysis<T> Analysis<T>(this IGraphQueryable<T> source)
        where T : class, IEntity, new()
    {
        // Implementation would be provider-specific
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }
}

/// <summary>
/// Extension methods for aggregation operations
/// </summary>
public static class GraphAggregationExtensions
{
    /// <summary>
    /// Counts connected entities of a specific type
    /// </summary>
    /// <typeparam name="TSource">The type of source entities</typeparam>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <typeparam name="TTarget">The type of target entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <returns>A queryable with connection counts</returns>
    public static IGraphQueryable<TSource> WithConnectionCount<TSource, TRel, TTarget>(
        this IQueryable<TSource> source)
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new()
    {
        // This would require provider-specific implementation to add computed properties
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }

    /// <summary>
    /// Groups entities by their connection patterns
    /// </summary>
    /// <typeparam name="T">The type of entities</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="connectionType">The type of connections to group by</param>
    /// <returns>A queryable grouped by connection patterns</returns>
    public static IGraphQueryable<IGrouping<int, T>> GroupByConnectionCount<T>(
        this IQueryable<T> source,
        Type connectionType)
        where T : class, INode, new()
    {
        // This would require provider-specific implementation
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }

    /// <summary>
    /// Calculates aggregate statistics over connected entities
    /// </summary>
    /// <typeparam name="TSource">The type of source entities</typeparam>
    /// <typeparam name="TTarget">The type of target entities</typeparam>
    /// <typeparam name="TResult">The type of aggregation result</typeparam>
    /// <param name="source">The source queryable</param>
    /// <param name="selector">The selection expression for connected entities</param>
    /// <param name="aggregator">The aggregation expression</param>
    /// <returns>A queryable with aggregated results</returns>
    public static IGraphQueryable<TResult> AggregateConnected<TSource, TTarget, TResult>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, IEnumerable<TTarget>>> selector,
        Expression<Func<IEnumerable<TTarget>, TResult>> aggregator)
        where TSource : class, IEntity, new()
        where TTarget : class, IEntity, new()
        where TResult : class
    {
        // This would require provider-specific implementation
        throw new NotImplementedException("This extension requires a graph provider implementation");
    }
}
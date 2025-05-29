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
/// Interface for building graph traversal queries from a starting point
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TRel">The type of the relationship being traversed</typeparam>
public interface IGraphTraversal<TSource, TRel> 
    where TSource : class, INode, new()
    where TRel : class, IRelationship, new()
{
    /// <summary>
    /// Specifies the direction of traversal for this relationship
    /// </summary>
    /// <param name="direction">The traversal direction</param>
    /// <returns>A traversal with the specified direction</returns>
    IGraphTraversal<TSource, TRel> InDirection(TraversalDirection direction);

    /// <summary>
    /// Filters relationships based on a predicate
    /// </summary>
    /// <param name="predicate">The predicate to apply to relationships</param>
    /// <returns>A traversal with the specified relationship filter</returns>
    IGraphTraversal<TSource, TRel> WhereRelationship(Expression<Func<TRel, bool>> predicate);

    /// <summary>
    /// Limits the depth of this traversal
    /// </summary>
    /// <param name="minDepth">The minimum depth to traverse</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A traversal with the specified depth limits</returns>
    IGraphTraversal<TSource, TRel> WithDepth(int minDepth, int maxDepth);

    /// <summary>
    /// Sets the maximum depth of this traversal
    /// </summary>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A traversal with the specified maximum depth</returns>
    IGraphTraversal<TSource, TRel> WithDepth(int maxDepth);

    /// <summary>
    /// Continues traversal to a specific target node type
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A queryable for the target nodes</returns>
    IGraphQueryable<TTarget> To<TTarget>() where TTarget : class, INode, new();

    /// <summary>
    /// Continues traversal to target nodes with filtering
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <param name="predicate">The predicate to apply to target nodes</param>
    /// <returns>A queryable for the filtered target nodes</returns>
    IGraphQueryable<TTarget> To<TTarget>(Expression<Func<TTarget, bool>> predicate) 
        where TTarget : class, INode, new();

    /// <summary>
    /// Returns the relationships themselves rather than traversing to targets
    /// </summary>
    /// <returns>A queryable for the relationships</returns>
    IGraphQueryable<TRel> Relationships();

    /// <summary>
    /// Returns the traversal paths including source, relationship, and target
    /// </summary>
    /// <typeparam name="TTarget">The type of target nodes</typeparam>
    /// <returns>A queryable for the complete paths</returns>
    IQueryable<IGraphPath<TSource, TRel, TTarget>> Paths<TTarget>() 
        where TTarget : class, INode, new();

    /// <summary>
    /// Continues traversal with another relationship type
    /// </summary>
    /// <typeparam name="TNextRel">The type of the next relationship</typeparam>
    /// <returns>A new traversal for the chained relationship</returns>
    IGraphTraversal<TSource, TNextRel> ThenTraverse<TNextRel>() 
        where TNextRel : class, IRelationship, new();

    /// <summary>
    /// Applies traversal options to this traversal
    /// </summary>
    /// <param name="options">The traversal options to apply</param>
    /// <returns>A traversal with the specified options</returns>
    IGraphTraversal<TSource, TRel> WithOptions(TraversalOptions options);
}

/// <summary>
/// Represents a complete path through the graph including source, relationship, and target
/// </summary>
/// <typeparam name="TSource">The type of the source node</typeparam>
/// <typeparam name="TRel">The type of the relationship</typeparam>
/// <typeparam name="TTarget">The type of the target node</typeparam>
public interface IGraphPath<TSource, TRel, TTarget>
    where TSource : class, INode, new()
    where TRel : class, IRelationship, new()
    where TTarget : class, INode, new()
{
    /// <summary>
    /// Gets the source node of this path
    /// </summary>
    TSource Source { get; }

    /// <summary>
    /// Gets the relationship in this path
    /// </summary>
    TRel Relationship { get; }

    /// <summary>
    /// Gets the target node of this path
    /// </summary>
    TTarget Target { get; }

    /// <summary>
    /// Gets the length of this path (number of hops)
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the total weight of this path if weights are defined
    /// </summary>
    double? Weight { get; }

    /// <summary>
    /// Gets metadata about this path
    /// </summary>
    IGraphPathMetadata Metadata { get; }
}

/// <summary>
/// Interface for multi-hop graph paths
/// </summary>
public interface IGraphMultiPath
{
    /// <summary>
    /// Gets all nodes in this path in order
    /// </summary>
    IReadOnlyList<INode> Nodes { get; }

    /// <summary>
    /// Gets all relationships in this path in order
    /// </summary>
    IReadOnlyList<IRelationship> Relationships { get; }

    /// <summary>
    /// Gets the length of this path (number of hops)
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the total weight of this path if weights are defined
    /// </summary>
    double? Weight { get; }

    /// <summary>
    /// Gets the source node (first node in the path)
    /// </summary>
    INode Source { get; }

    /// <summary>
    /// Gets the target node (last node in the path)
    /// </summary>
    INode Target { get; }

    /// <summary>
    /// Gets metadata about this path
    /// </summary>
    IGraphPathMetadata Metadata { get; }
}

/// <summary>
/// Metadata information about a graph path
/// </summary>
public interface IGraphPathMetadata
{
    /// <summary>
    /// Gets the unique identifier for this path
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the cost metrics for this path
    /// </summary>
    IGraphPathCost Cost { get; }

    /// <summary>
    /// Gets additional properties associated with this path
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Cost metrics for a graph path
/// </summary>
public interface IGraphPathCost
{
    /// <summary>
    /// Gets the total distance/weight of the path
    /// </summary>
    double Distance { get; }

    /// <summary>
    /// Gets the number of hops in the path
    /// </summary>
    int Hops { get; }

    /// <summary>
    /// Gets the computation cost of finding this path
    /// </summary>
    double ComputationCost { get; }
}

/// <summary>
/// Direction for graph traversal
/// </summary>
public enum TraversalDirection
{
    /// <summary>Follow outgoing relationships</summary>
    Outgoing,
    
    /// <summary>Follow incoming relationships</summary>
    Incoming,
    
    /// <summary>Follow relationships in both directions</summary>
    Both,
    
    /// <summary>Use the natural direction of the relationship</summary>
    Natural
}

/// <summary>
/// Options for controlling graph traversal behavior
/// </summary>
public class TraversalOptions
{
    /// <summary>
    /// Gets or sets the maximum depth to traverse
    /// </summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum depth to traverse
    /// </summary>
    public int MinDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to include the starting nodes in results
    /// </summary>
    public bool IncludeStartNodes { get; set; } = false;

    /// <summary>
    /// Gets or sets the traversal direction
    /// </summary>
    public TraversalDirection Direction { get; set; } = TraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets whether to avoid cycles during traversal
    /// </summary>
    public bool AvoidCycles { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of results to return
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Gets or sets whether to compute path weights
    /// </summary>
    public bool ComputeWeights { get; set; } = false;

    /// <summary>
    /// Gets or sets the property name to use for edge weights
    /// </summary>
    public string? WeightProperty { get; set; }

    /// <summary>
    /// Gets or sets the traversal strategy
    /// </summary>
    public TraversalStrategy Strategy { get; set; } = TraversalStrategy.DepthFirst;

    /// <summary>
    /// Gets or sets additional traversal hints
    /// </summary>
    public List<string> Hints { get; set; } = new();
}

/// <summary>
/// Strategy for traversing the graph
/// </summary>
public enum TraversalStrategy
{
    /// <summary>Use depth-first traversal</summary>
    DepthFirst,
    
    /// <summary>Use breadth-first traversal</summary>
    BreadthFirst,
    
    /// <summary>Use shortest path algorithm</summary>
    ShortestPath,
    
    /// <summary>Use weighted shortest path algorithm</summary>
    WeightedShortestPath,
    
    /// <summary>Let the provider choose the optimal strategy</summary>
    Automatic
}
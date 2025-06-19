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
/// Extension methods for graph traversal operations built on top of PathSegments.
/// These methods provide convenient ways to traverse relationships and get target nodes or relationships.
/// </summary>
public static class GraphTraversalExtensions
{
    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphNodeQueryable<TStartNode> source)
        where TStartNode : INode
        where TRelationship : IRelationship
        where TEndNode : INode
    {
        return source
            .PathSegments<TRelationship, TEndNode>()
            .Select(ps => ps.EndNode);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with depth constraints.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphNodeQueryable<TStartNode> source,
        int maxDepth)
        where TStartNode : INode
        where TRelationship : IRelationship
        where TEndNode : INode
    {
        return source
            .PathSegments<TRelationship, TEndNode>()
            .WithDepth(maxDepth)
            .Select(ps => ps.EndNode);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with depth range constraints.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="minDepth">The minimum depth to traverse</param>
    /// <param name="maxDepth">The maximum depth to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphNodeQueryable<TStartNode> source,
        int minDepth,
        int maxDepth)
        where TStartNode : INode
        where TRelationship : IRelationship
        where TEndNode : INode
    {
        return source
            .PathSegments<TRelationship, TEndNode>()
            .WithDepth(minDepth, maxDepth)
            .Select(ps => ps.EndNode);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the target nodes with direction constraints.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <param name="direction">The direction to traverse</param>
    /// <returns>A queryable of target nodes reached through the traversal</returns>
    public static IGraphQueryable<TEndNode> Traverse<TStartNode, TRelationship, TEndNode>(
        this IGraphNodeQueryable<TStartNode> source,
        TraversalDirection direction)
        where TStartNode : INode
        where TRelationship : IRelationship
        where TEndNode : INode
    {
        return source
            .PathSegments<TRelationship, TEndNode>()
            .InDirection(direction)
            .Select(ps => ps.EndNode);
    }

    /// <summary>
    /// Traverses relationships of the specified type to get the relationships.
    /// This is implemented as a convenience method on top of PathSegments.
    /// </summary>
    /// <typeparam name="TStartNode">The type of the starting nodes</typeparam>
    /// <typeparam name="TRelationship">The type of relationships to traverse</typeparam>
    /// <typeparam name="TEndNode">The type of the target nodes</typeparam>
    /// <param name="source">The source queryable of starting nodes</param>
    /// <returns>A queryable of relationships traversed</returns>
    public static IGraphQueryable<TRelationship> TraverseRelationships<TStartNode, TRelationship, TEndNode>(
        this IGraphNodeQueryable<TStartNode> source)
        where TStartNode : INode
        where TRelationship : IRelationship
        where TEndNode : INode
    {
        return source
            .PathSegments<TRelationship, TEndNode>()
            .Select(ps => ps.Relationship);
    }
}

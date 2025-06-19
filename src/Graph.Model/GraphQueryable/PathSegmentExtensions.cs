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
/// Extension methods for path segment queries that provide traversal features like depth constraints and direction.
/// </summary>
public static class PathSegmentExtensions
{
    /// <summary>
    /// Adds depth constraints to a path segment query.
    /// </summary>
    /// <typeparam name="TNode">The starting node type</typeparam>
    /// <typeparam name="TRel">The relationship type</typeparam>
    /// <typeparam name="TTarget">The target node type</typeparam>
    /// <param name="pathSegments">The path segments query</param>
    /// <param name="maxDepth">Maximum depth to traverse</param>
    /// <returns>A path segments query with depth constraints</returns>
    public static IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> WithDepth<TNode, TRel, TTarget>(
        this IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> pathSegments,
        int maxDepth)
        where TNode : INode
        where TRel : IRelationship
        where TTarget : INode
    {
        return WithDepth(pathSegments, 1, maxDepth);
    }

    /// <summary>
    /// Adds depth range constraints to a path segment query.
    /// </summary>
    /// <typeparam name="TNode">The starting node type</typeparam>
    /// <typeparam name="TRel">The relationship type</typeparam>
    /// <typeparam name="TTarget">The target node type</typeparam>
    /// <param name="pathSegments">The path segments query</param>
    /// <param name="minDepth">Minimum depth to traverse</param>
    /// <param name="maxDepth">Maximum depth to traverse</param>
    /// <returns>A path segments query with depth constraints</returns>
    public static IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> WithDepth<TNode, TRel, TTarget>(
        this IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> pathSegments,
        int minDepth,
        int maxDepth)
        where TNode : INode
        where TRel : IRelationship
        where TTarget : INode
    {
        if (minDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(minDepth), "Minimum depth must be non-negative");
        if (maxDepth < minDepth)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Maximum depth must be greater than or equal to minimum depth");

        // Create a method call expression that represents WithDepth on the path segments
        var methodCall = Expression.Call(
            typeof(PathSegmentExtensions),
            nameof(WithDepth),
            new[] { typeof(TNode), typeof(TRel), typeof(TTarget) },
            pathSegments.Expression,
            Expression.Constant(minDepth),
            Expression.Constant(maxDepth));

        return pathSegments.Provider.CreatePathSegmentQuery<TNode, TRel, TTarget>(methodCall);
    }

    /// <summary>
    /// Adds direction constraints to a path segment query.
    /// </summary>
    /// <typeparam name="TNode">The starting node type</typeparam>
    /// <typeparam name="TRel">The relationship type</typeparam>
    /// <typeparam name="TTarget">The target node type</typeparam>
    /// <param name="pathSegments">The path segments query</param>
    /// <param name="direction">The traversal direction</param>
    /// <returns>A path segments query with direction constraints</returns>
    public static IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> InDirection<TNode, TRel, TTarget>(
        this IGraphQueryable<IGraphPathSegment<TNode, TRel, TTarget>> pathSegments,
        TraversalDirection direction)
        where TNode : INode
        where TRel : IRelationship
        where TTarget : INode
    {
        // Create a method call expression that represents InDirection on the path segments
        var methodCall = Expression.Call(
            typeof(PathSegmentExtensions),
            nameof(InDirection),
            new[] { typeof(TNode), typeof(TRel), typeof(TTarget) },
            pathSegments.Expression,
            Expression.Constant(direction));

        return pathSegments.Provider.CreatePathSegmentQuery<TNode, TRel, TTarget>(methodCall);
    }
}

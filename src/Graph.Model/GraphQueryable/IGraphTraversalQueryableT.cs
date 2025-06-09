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
/// Defines a queryable for traversing graph relationships from a source node type, through a relationship type, to a target node type.
/// Enables fluent traversal, relationship and node filtering, and full LINQ compatibility.
/// </summary>
/// <typeparam name="TSource">The type of the source node.</typeparam>
/// <typeparam name="TRel">The type of the relationship being traversed.</typeparam>
/// <typeparam name="TTarget">The type of the target node.</typeparam>
public interface IGraphTraversalQueryable<TSource, TRel, TTarget> : IGraphQueryable<TTarget>, IGraphTraversalQueryable
    where TSource : INode
    where TRel : IRelationship
    where TTarget : INode
{
    /// <summary>
    /// Specifies the direction of traversal for the relationship (outgoing, incoming, or both).
    /// </summary>
    /// <param name="direction">The direction to traverse the relationship.</param>
    /// <returns>A new traversal queryable with the specified direction.</returns>
    IGraphTraversalQueryable<TSource, TRel, TTarget> InDirection(TraversalDirection direction);

    /// <summary>
    /// Limits the traversal to a fixed maximum depth (number of hops).
    /// </summary>
    /// <param name="maxDepth">The maximum depth to traverse.</param>
    /// <returns>A new traversal queryable with the specified maximum depth.</returns>
    IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int maxDepth);

    /// <summary>
    /// Limits the traversal to a range of depths (inclusive).
    /// </summary>
    /// <param name="minDepth">The minimum depth to traverse.</param>
    /// <param name="maxDepth">The maximum depth to traverse.</param>
    /// <returns>A new traversal queryable with the specified depth range.</returns>
    IGraphTraversalQueryable<TSource, TRel, TTarget> WithDepth(int minDepth, int maxDepth);

    /// <summary>
    /// Applies advanced traversal options (e.g., uniqueness, evaluation).
    /// </summary>
    /// <param name="options">The traversal options to apply.</param>
    /// <returns>A new traversal queryable with the options applied.</returns>
    IGraphTraversalQueryable<TSource, TRel, TTarget> WithOptions(TraversalOptions options);

    /// <summary>
    /// Continues traversal with another relationship type and target node type.
    /// Enables chaining of multi-hop traversals in a fluent manner.
    /// </summary>
    /// <typeparam name="TNextRel">The type of the next relationship to traverse.</typeparam>
    /// <typeparam name="TNextTarget">The type of the next target node.</typeparam>
    /// <returns>A traversal queryable representing the next hop.</returns>
    IGraphTraversalQueryable<TSource, TNextRel, TNextTarget> ThenTraverse<TNextRel, TNextTarget>()
        where TNextRel : IRelationship
        where TNextTarget : INode;
}
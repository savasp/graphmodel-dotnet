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

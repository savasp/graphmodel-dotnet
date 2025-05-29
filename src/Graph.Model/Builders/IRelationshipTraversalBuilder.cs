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

// <summary>
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

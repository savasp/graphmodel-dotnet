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


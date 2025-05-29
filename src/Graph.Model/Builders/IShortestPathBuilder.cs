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

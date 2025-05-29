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
/// Builder for group traversal operations
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TKey">The type of the grouping key</typeparam>
public interface IGroupTraversalBuilder<TSource, TKey> where TSource : class, IEntity, new()
{
    /// <summary>
    /// Applies a filter to the grouped results
    /// </summary>
    /// <param name="predicate">The group filter</param>
    /// <returns>A group traversal builder with the filter applied</returns>
    IGroupTraversalBuilder<TSource, TKey> Having(Expression<Func<IGrouping<TKey, TSource>, bool>> predicate);

    /// <summary>
    /// Projects the grouped results
    /// </summary>
    /// <typeparam name="TResult">The type of the projection result</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the projected group results</returns>
    IGraphQueryable<TResult> Select<TResult>(Expression<Func<IGrouping<TKey, TSource>, TResult>> selector);

    /// <summary>
    /// Orders the groups by a key
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> OrderBy<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);

    /// <summary>
    /// Orders the groups by a key in descending order
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> OrderByDescending<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);
}

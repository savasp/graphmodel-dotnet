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
/// Ordered group traversal builder interface
/// </summary>
/// <typeparam name="TSource">The type of the source entity</typeparam>
/// <typeparam name="TKey">The type of the grouping key</typeparam>
public interface IOrderedGroupTraversalBuilder<TSource, TKey> : IGroupTraversalBuilder<TSource, TKey>
    where TSource : class, IEntity, new()
{
    /// <summary>
    /// Applies a secondary ordering to the groups
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder with secondary ordering</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> ThenBy<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);

    /// <summary>
    /// Applies a secondary ordering to the groups in descending order
    /// </summary>
    /// <typeparam name="TOrderKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered group traversal builder with secondary descending ordering</returns>
    IOrderedGroupTraversalBuilder<TSource, TKey> ThenByDescending<TOrderKey>(Expression<Func<IGrouping<TKey, TSource>, TOrderKey>> keySelector);
}
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
/// Ordered query builder interface
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
public interface IOrderedGraphQueryBuilder<T> : IGraphQueryBuilder<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Applies a secondary ordering
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder with the secondary ordering</returns>
    IOrderedGraphQueryBuilder<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector);

    /// <summary>
    /// Applies a secondary ordering in descending order
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>An ordered query builder with the secondary descending ordering</returns>
    IOrderedGraphQueryBuilder<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
}
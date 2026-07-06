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
/// Represents a sorted relationship queryable that supports additional ordering operations.
/// </summary>
/// <remarks>
/// Obsolete: use <see cref="IOrderedGraphQueryable{T}"/> instead. Kept for one release to ease migration.
/// </remarks>
/// <typeparam name="TRel">The type of relationship.</typeparam>
[Obsolete("Use IOrderedGraphQueryable<T> instead. This alias will be removed in a future release.")]
#pragma warning disable CS0618 // Type or member is obsolete
public interface IOrderedGraphRelationshipQueryable<out TRel> : IGraphRelationshipQueryable<TRel>, IOrderedQueryable<TRel>
#pragma warning restore CS0618
    where TRel : class, IRelationship
{
}
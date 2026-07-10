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

namespace Cvoya.Graph;

/// <summary>
/// Non-generic base interface for relationship queryables.
/// </summary>
/// <remarks>
/// Obsolete: node/relationship queryables are unified as <see cref="IGraphQueryable{T}"/>;
/// graph operators are gated by generic constraints instead of a receiver interface hierarchy.
/// This alias is kept for one release to ease migration and will be removed afterwards.
/// </remarks>
[Obsolete("Use IGraphQueryable<T> with a 'where T : IRelationship' constraint instead. This alias will be removed in a future release.")]
public interface IGraphRelationshipQueryable : IGraphQueryable
{
}
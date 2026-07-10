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
/// Represents a queryable graph data source that supports LINQ operations over nodes.
/// </summary>
/// <remarks>
/// Obsolete: use <see cref="IGraphQueryable{T}"/> directly (with <c>T : INode</c>). Traversal and
/// other node-only operators are now gated by generic constraints on the operator itself, so this
/// receiver interface is no longer needed. Kept for one release to ease migration.
/// </remarks>
/// <typeparam name="TNode">An <see cref="INode"/>-derived type.</typeparam>
[Obsolete("Use IGraphQueryable<T> instead; node-only operators are gated by generic constraints. This alias will be removed in a future release.")]
#pragma warning disable CS0618 // Type or member is obsolete
public interface IGraphNodeQueryable<out TNode> : IGraphQueryable<TNode>, IGraphNodeQueryable
#pragma warning restore CS0618
    where TNode : class, INode
{
}
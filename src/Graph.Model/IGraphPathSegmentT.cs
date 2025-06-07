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
/// Represents a strongly-typed direct connection between two nodes in a graph.
/// </summary>
/// <typeparam name="TSource">The type of the source node.</typeparam>
/// <typeparam name="TRel">The type of the relationship.</typeparam>
/// <typeparam name="TTarget">The type of the target node.</typeparam>
public interface IGraphPathSegment<TSource, TRel, TTarget> : IGraphPathSegment
    where TSource : INode
    where TRel : IRelationship
    where TTarget : INode
{
    /// <summary>
    /// Gets the strongly-typed starting node of the path segment.
    /// </summary>
    new TSource StartNode { get; }

    /// <summary>
    /// Gets the strongly-typed ending node of the path segment.
    /// </summary>
    new TTarget EndNode { get; }

    /// <summary>
    /// Gets the strongly-typed relationship connecting the nodes.
    /// </summary>
    new TRel Relationship { get; }
}
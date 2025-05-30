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
/// Represents a complete path through the graph including source, relationship, and target
/// </summary>
/// <typeparam name="TSource">The type of the source node</typeparam>
/// <typeparam name="TRel">The type of the relationship</typeparam>
/// <typeparam name="TTarget">The type of the target node</typeparam>
public interface IGraphPath<TSource, TRel, TTarget>
    where TSource : class, INode, new()
    where TRel : class, IRelationship, new()
    where TTarget : class, INode, new()
{
    /// <summary>
    /// Gets the source node of this path
    /// </summary>
    TSource Source { get; }

    /// <summary>
    /// Gets the relationship in this path
    /// </summary>
    TRel Relationship { get; }

    /// <summary>
    /// Gets the target node of this path
    /// </summary>
    TTarget Target { get; }

    /// <summary>
    /// Gets the length of this path (number of hops)
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the total weight of this path if weights are defined
    /// </summary>
    double? Weight { get; }

    /// <summary>
    /// Gets metadata about this path
    /// </summary>
    IGraphPathMetadata Metadata { get; }
}

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
/// Interface for multi-hop graph paths
/// </summary>
public interface IGraphMultiPath
{
    /// <summary>
    /// Gets all nodes in this path in order
    /// </summary>
    IReadOnlyList<INode> Nodes { get; }

    /// <summary>
    /// Gets all relationships in this path in order
    /// </summary>
    IReadOnlyList<IRelationship> Relationships { get; }

    /// <summary>
    /// Gets the length of this path (number of hops)
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets the total weight of this path if weights are defined
    /// </summary>
    double? Weight { get; }

    /// <summary>
    /// Gets the source node (first node in the path)
    /// </summary>
    INode Source { get; }

    /// <summary>
    /// Gets the target node (last node in the path)
    /// </summary>
    INode Target { get; }

    /// <summary>
    /// Gets metadata about this path
    /// </summary>
    IGraphPathMetadata Metadata { get; }
}

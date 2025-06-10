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
/// Represents a direct connection between two nodes in a graph, encapsulating the relationship and its endpoints.
/// This interface is used to model segments of paths in a graph, where each segment consists of a start node, an end node, and the relationship connecting them.
/// It is typically used in graph traversal and pathfinding algorithms to represent the individual steps in a path.
/// </summary>
public interface IGraphPathSegment
{
    /// <summary>
    /// Gets the starting node of the path segment.
    /// </summary>
    INode StartNode { get; }

    /// <summary>
    /// Gets the ending node of the path segment.
    /// </summary>
    INode EndNode { get; }

    /// <summary>
    /// Gets the relationship connecting the start and end nodes.
    /// </summary>
    IRelationship Relationship { get; }
}
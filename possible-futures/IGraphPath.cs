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
public interface IGraphPath : IReadOnlyList<IRelationship>
{
    /// <summary>
    /// Gets the first node in the path (start node).
    /// </summary>
    INode StartNode { get; }

    /// <summary>
    /// Gets the last node in the path (end node).
    /// </summary>
    INode EndNode { get; }
}
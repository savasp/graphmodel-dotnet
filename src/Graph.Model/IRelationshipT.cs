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
/// Represents a directional relationship with a specific type.
/// </summary>
/// <typeparam name="S">The type of the source node in the relationship</typeparam>
/// <typeparam name="T">The type of the target node in the relationship</typeparam>
public interface IRelationship<S, T> : IRelationship
    where S : INode
    where T : INode
{
    /// <summary>
    /// Gets the source node of the relationship.
    /// </summary>
    S Source { get; set; }

    /// <summary>
    /// Gets the target node of the relationship.
    /// </summary>
    T Target { get; set; }
}
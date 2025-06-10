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
/// Defines a strongly-typed relationship between two specific node types.
/// </summary>
/// <typeparam name="S">The type of the source node in the relationship.</typeparam>
/// <typeparam name="T">The type of the target node in the relationship.</typeparam>
/// <remarks>
/// This interface extends <see cref="IRelationship"/> by adding strongly-typed references
/// to the actual source and target node objects, facilitating more type-safe graph traversal.
/// </remarks>
public interface IRelationship<S, T> : IRelationship
    where S : class, INode, new()
    where T : class, INode, new()
{
    /// <summary>
    /// Gets or sets the source node of the relationship.
    /// </summary>
    /// <remarks>
    /// When set, this also updates the <see cref="IRelationship.StartNodeId"/> property.
    /// May be null if the relationship is not fully loaded.
    /// </remarks>
    S Source { get; set; }

    /// <summary>
    /// Gets or sets the target node of the relationship.
    /// </summary>
    /// <remarks>
    /// When set, this also updates the <see cref="IRelationship.EndNodeId"/> property.
    /// May be null if the relationship is not fully loaded.
    /// </remarks>
    T Target { get; set; }
}
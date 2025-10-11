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
/// Defines the contract for node entities in the graph model.
/// Nodes represent primary data entities that can be connected via relationships.
/// This interface serves as a marker interface that extends IEntity, 
/// signifying that implementing classes represent nodes rather than relationships.
/// </summary>
/// <remarks>
/// Implement this interface on classes that represent domain entities in your graph model.
/// Typically used with the <see cref="NodeAttribute"/> to define node metadata.
/// </remarks>
public interface INode : IEntity
{
    /// <summary>
    /// Gets the labels for this node as they are stored in the graph database.
    /// This is a runtime property that reflects the actual labels assigned to the node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is automatically populated by the graph provider when the node is
    /// retrieved from or saved to the database. The labels are derived from the <see cref="NodeAttribute"/>
    /// on the implementing type, or the type name if no attribute is present.
    /// </para>
    /// <para>
    /// This property enables polymorphic queries and filtering by label at runtime,
    /// complementing the compile-time type system.
    /// </para>
    /// <para>
    /// Do not set this property manually - it is managed by the graph provider.
    /// </para>
    /// </remarks>
    IReadOnlyList<string> Labels { get; }
}

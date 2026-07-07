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
/// Defines the contract for relationship entities in the graph model.
/// Relationships connect two nodes and can have their own properties.
/// </summary>
/// <remarks>
/// Relationships form the connections between nodes, creating the graph structure.
/// </remarks>
public interface IRelationship : IEntity
{
    /// <summary>
    /// Gets the type of this relationship as it is stored in the graph database.
    /// This is a runtime property that reflects the actual relationship type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is automatically populated by the graph provider when the relationship is
    /// retrieved from or saved to the database. The type is derived from the <see cref="RelationshipAttribute"/>
    /// on the implementing type, or the type name if no attribute is present.
    /// </para>
    /// <para>
    /// This property enables polymorphic queries and filtering by type at runtime,
    /// complementing the compile-time type system.
    /// </para>
    /// <para>
    /// Do not set this property manually - it is managed by the graph provider.
    /// </para>
    /// </remarks>
    string Type { get; }

    /// <summary>
    /// Gets the physical storage direction of this relationship.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="RelationshipDirection.Outgoing"/> means the stored edge points from
    /// <see cref="StartNodeId"/> to <see cref="EndNodeId"/>. <see cref="RelationshipDirection.Incoming"/>
    /// means the stored edge points from <see cref="EndNodeId"/> to <see cref="StartNodeId"/>.
    /// </para>
    /// <para>
    /// This is storage metadata, not traversal configuration. Use <see cref="GraphTraversalDirection"/>
    /// when choosing how a query traverses relationships.
    /// </para>
    /// </remarks>
    RelationshipDirection Direction { get; init; }

    /// <summary>
    /// Gets the ID of the start node in this relationship.
    /// </summary>
    /// <remarks>
    /// This is the first endpoint in the relationship's logical node tuple. The stored edge
    /// originates here only when <see cref="Direction"/> is <see cref="RelationshipDirection.Outgoing"/>.
    /// </remarks>
    string StartNodeId { get; init; }

    /// <summary>
    /// Gets the ID of the end node in this relationship.
    /// </summary>
    /// <remarks>
    /// This is the second endpoint in the relationship's logical node tuple. The stored edge
    /// points here only when <see cref="Direction"/> is <see cref="RelationshipDirection.Outgoing"/>.
    /// </remarks>
    string EndNodeId { get; init; }
}

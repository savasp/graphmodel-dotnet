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
/// Base class for graph relationships that provides a default implementation of the IRelationship interface.
/// This serves as a foundation for creating domain-specific relationship entities.
/// </summary>
/// <param name="StartNodeId">The ID of the start node in the relationship.</param>
/// <param name="EndNodeId">The ID of the end node in the relationship.</param>
/// <param name="Direction">The direction of the relationship, defaulting to <see cref="RelationshipDirection.Outgoing"/>.</param>
/// <remarks>
/// Use this class as a base class for your domain-specific relationship models to get automatic ID generation
/// and basic relationship functionality.
/// </remarks>
public abstract record Relationship(string StartNodeId, string EndNodeId, RelationshipDirection Direction = RelationshipDirection.Outgoing) : IRelationship
{
    /// <inheritdoc/>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the type of this relationship as it is stored in the graph database.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the graph provider when the relationship is
    /// created or retrieved from the database. The type is derived from the <see cref="RelationshipAttribute"/>
    /// or the type name if no attribute is present.
    /// </remarks>
    public virtual string Type { get; set; } = string.Empty;
}

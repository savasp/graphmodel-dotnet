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
/// Concrete implementation of a dynamic relationship.
/// </summary>
public sealed record DynamicRelationship : Relationship
{
    /// <summary>
    /// Gets the type of this relationship.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the properties of this relationship.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicRelationship"/> class.
    /// </summary>
    public DynamicRelationship() : base(string.Empty, string.Empty, RelationshipDirection.Outgoing) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicRelationship"/> class with specified parameters.
    /// </summary>
    /// <param name="startNodeId">The ID of the start node.</param>
    /// <param name="endNodeId">The ID of the end node.</param>
    /// <param name="type">The type of the relationship.</param>
    /// <param name="properties">The properties of the relationship.</param>
    /// <param name="direction">The direction of the relationship.</param>
    public DynamicRelationship(string startNodeId, string endNodeId, string type,
        IDictionary<string, object?> properties, RelationshipDirection direction = RelationshipDirection.Outgoing) : base(startNodeId, endNodeId, direction)
    {
        Type = type;
        Properties = new Dictionary<string, object?>(properties).AsReadOnly();
    }
}
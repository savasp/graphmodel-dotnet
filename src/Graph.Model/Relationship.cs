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
/// <remarks>
/// Use this class as a base class for your domain-specific relationship models to get automatic ID generation
/// and basic relationship functionality.
/// </remarks>
[Relationship("GENERIC")]
public abstract class Relationship : IRelationship
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Relationship"/> class with specified directionality.
    /// </summary>
    /// <param name="sourceId">The ID of the source node in the relationship.</param>
    /// <param name="targetId">The ID of the target node in the relationship.</param>
    /// <param name="isBidirectional">Indicates whether the relationship is bidirectional.</param>
    /// <remarks>
    /// Set isBidirectional to true if the relationship can be traversed in both directions.
    /// </remarks>
    public Relationship(string? sourceId = null, string? targetId = null, bool isBidirectional = false)
    {
        SourceId = sourceId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        IsBidirectional = isBidirectional;
    }

    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string SourceId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string TargetId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public bool IsBidirectional { get; set; }
}

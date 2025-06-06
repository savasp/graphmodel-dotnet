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
/// <param name="SourceId">The ID of the source node in the relationship.</param>
/// <param name="TargetId">The ID of the target node in the relationship.</param>
/// <param name="IsBidirectional">Indicates whether the relationship is bidirectional.</param>
/// <remarks>
/// Use this class as a base class for your domain-specific relationship models to get automatic ID generation
/// and basic relationship functionality.
/// </remarks>
public abstract record Relationship(string SourceId, string TargetId, bool IsBidirectional = false) : IRelationship
{
    /// <inheritdoc/>
    public string Id { get; } = Guid.NewGuid().ToString("N");
}

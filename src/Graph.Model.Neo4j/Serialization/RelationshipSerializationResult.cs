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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Contains the result of serializing a relationship.
/// </summary>
internal record RelationshipSerializationResult
{
    /// <summary>
    /// The properties to store on the Neo4j relationship.
    /// </summary>
    public required Dictionary<string, object?> Properties { get; init; }

    /// <summary>
    /// The relationship type/label.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The source node ID.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// The target node ID.
    /// </summary>
    public required string TargetId { get; init; }
}
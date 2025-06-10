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
/// Attribute to customize aspects of a relationship in the graph.
/// </summary>
/// <remarks>
/// Use this attribute on classes implementing IRelationship to define how the relationship
/// should be labeled in the graph storage system.
/// </remarks>
/// <example>
/// <code>
/// [Relationship(Label = "FOLLOWS")]
/// public class Follows : IRelationship&lt;Person, Person&gt;
/// {
///     public string Id { get; set; } = Guid.NewGuid().ToString();
///     public string StartNodeId { get; set; } = string.Empty;
///     public string EndNodeId { get; set; } = string.Empty;
///     public bool Direction { get; set; }
///     public DateTime Since { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public class RelationshipAttribute() : Attribute
{
    /// <summary>
    /// Gets or sets the label to apply to the relationship. If null, the name of the class is used.
    /// </summary>
    /// <value>The relationship label used for graph storage.</value>
    public string? Label { get; set; } = null;
}
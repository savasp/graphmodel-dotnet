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
/// <param name="label">The label to apply to the relationship. Cannot be null.</param>
/// <remarks>
/// Use this attribute on classes implementing IRelationship to define how the relationship
/// should be labeled in the graph storage system.
/// </remarks>
/// <example>
/// <code>
/// [Relationship("FOLLOWS")]
/// public class Follows : IRelationship&lt;Person, Person&gt;
/// {
///     public string Id { get; set; } = Guid.NewGuid().ToString();
///     public string SourceId { get; set; } = string.Empty;
///     public string TargetId { get; set; } = string.Empty;
///     public bool IsBidirectional { get; set; }
///     public Person? Source { get; set; }
///     public Person? Target { get; set; }
///     public DateTime Since { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class RelationshipAttribute(string label) : Attribute
{
    /// <summary>
    /// Gets the label to apply to the relationship.
    /// </summary>
    /// <value>The relationship label used for graph storage.</value>
    public string Label { get; } = label ?? throw new ArgumentNullException(nameof(label));

    /// <summary>
    /// Gets or sets the direction of the relationship. 
    /// This can be used by graph providers to control relationship directionality.
    /// </summary>
    public RelationshipDirection Direction { get; set; } = RelationshipDirection.Outgoing;
}

/// <summary>
/// Defines the direction of a relationship in the graph.
/// </summary>
public enum RelationshipDirection
{
    /// <summary>
    /// Relationship direction from source to target (->)
    /// </summary>
    Outgoing,

    /// <summary>
    /// Relationship direction from target to source (&lt;-)
    /// </summary>
    Incoming,

    /// <summary>
    /// Relationship is bidirectional (&lt;->)
    /// </summary>
    Bidirectional
}

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

using System;
using System.Linq;


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
    /// Initializes a new instance of the RelationshipAttribute class with the specified label.
    /// </summary>
    /// <param name="label">The label to apply to the relationship.</param>
    public RelationshipAttribute(string label) : this()
    {
        Label = label;
    }

    /// <summary>
    /// Initializes a new instance of the RelationshipAttribute class with multiple labels.
    /// </summary>
    /// <param name="labels">The labels to apply to the relationship.</param>
    public RelationshipAttribute(params string[] labels) : this()
    {
        if (labels.Length > 0)
        {
            Label = labels[0]; // Primary label
            AdditionalLabels = labels.Skip(1).ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the label to apply to the relationship. If null, the name of the class is used.
    /// </summary>
    /// <value>The relationship label used for graph storage.</value>
    public string? Label { get; set; } = null;

    /// <summary>
    /// Gets additional labels for the relationship.
    /// </summary>
    /// <value>Additional labels used for graph storage.</value>
    public string[] AdditionalLabels { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Gets all labels (primary + additional) for the relationship.
    /// </summary>
    /// <returns>All labels for this relationship.</returns>
    public IEnumerable<string> GetAllLabels()
    {
        if (!string.IsNullOrEmpty(Label))
            yield return Label;

        foreach (var additionalLabel in AdditionalLabels)
        {
            if (!string.IsNullOrEmpty(additionalLabel))
                yield return additionalLabel;
        }
    }
}
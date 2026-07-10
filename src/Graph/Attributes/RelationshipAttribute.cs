// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System;


/// <summary>
/// Attribute to customize aspects of a relationship in the graph.
/// </summary>
/// <remarks>
/// Use this attribute on classes implementing IRelationship to define how the relationship is labeled in the
/// graph storage system. A relationship carries exactly one type; if the attribute is omitted (or its
/// <see cref="Label"/> is left unset), the class name is used. The type must be unique (case-insensitive)
/// across every relationship type loaded in the process; <see cref="SchemaRegistry"/> enforces this.
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
    /// Gets or sets the label to apply to the relationship. If null, the name of the class is used.
    /// </summary>
    /// <value>The relationship label used for graph storage.</value>
    public string? Label { get; set; } = null;
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Concrete implementation of a dynamic relationship.
/// </summary>
public sealed record DynamicRelationship : Relationship
{
    /// <summary>
    /// Gets or sets the type of this relationship.
    /// </summary>
    public override string Type { get; set; } = string.Empty;

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
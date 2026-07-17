// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Concrete implementation of a dynamic node.
/// </summary>
public sealed record DynamicNode : Node
{
    /// <summary>
    /// Gets or sets the labels for this node.
    /// </summary>
    public override IReadOnlyList<string> Labels { get; set; } = new List<string>();

    /// <summary>
    /// Gets the properties of this node.
    /// </summary>
    /// <remarks>
    /// Simple collection properties are materialized as <see cref="List{T}"/> values because the
    /// serialized representation retains the element type but not the caller's original collection type.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class.
    /// </summary>
    public DynamicNode() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class with specified labels and properties.
    /// </summary>
    /// <param name="labels">The labels for the node.</param>
    /// <param name="properties">The properties of the node.</param>
    public DynamicNode(IEnumerable<string> labels, IDictionary<string, object?> properties)
    {
        Labels = labels.ToList().AsReadOnly();
        Properties = new Dictionary<string, object?>(properties).AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class with specified ID, labels and properties.
    /// </summary>
    /// <param name="id">The unique identifier of the node.</param>
    /// <param name="labels">The labels for the node.</param>
    /// <param name="properties">The properties of the node.</param>
    public DynamicNode(string id, IEnumerable<string> labels, IDictionary<string, object?> properties)
    {
        Id = id;
        Labels = labels.ToList().AsReadOnly();
        Properties = new Dictionary<string, object?>(properties).AsReadOnly();
    }
}

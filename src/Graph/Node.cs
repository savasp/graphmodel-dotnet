// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Base class for graph nodes that provides a default implementation of the INode interface.
/// This serves as a foundation for creating domain-specific node entities.
/// </summary>
/// <remarks>
/// Use this class as a base class for your domain models to get automatic ID generation
/// and basic node functionality.
/// </remarks>
public abstract record Node : INode
{
    /// <summary>
    /// Gets or sets the unique identifier of this node.
    /// Automatically initialized with a new GUID string when a node is created.
    /// </summary>
    /// <remarks>
    /// The default format used is the "N" format (32 digits without hyphens).
    /// </remarks>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the labels for this node as they are stored in the graph database.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the graph provider when the node is
    /// created or retrieved from the database. The labels are derived from the <see cref="NodeAttribute"/>
    /// or the type name if no attribute is present.
    /// </remarks>
    public virtual IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
}

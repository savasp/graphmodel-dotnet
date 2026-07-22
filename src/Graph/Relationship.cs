// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Base class for graph relationships that provides a default implementation of the IRelationship interface.
/// This serves as a foundation for creating domain-specific relationship entities.
/// </summary>
/// <remarks>
/// Use this class as a base class for domain relationship models.
/// </remarks>
public abstract record Relationship : IRelationship
{
    /// <summary>
    /// Gets or sets the type of this relationship as it is stored in the graph database.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the graph provider when the relationship is
    /// created or retrieved from the database. The type is derived from the <see cref="RelationshipAttribute"/>
    /// or the type name if no attribute is present.
    /// </remarks>
    public virtual string Type { get; set; } = string.Empty;
}

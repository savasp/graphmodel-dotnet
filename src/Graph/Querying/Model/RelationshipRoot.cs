// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Represents a relationship query root.
/// </summary>
public sealed record RelationshipRoot : QueryRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipRoot"/> record.
    /// </summary>
    /// <param name="elementType">The relationship element type.</param>
    public RelationshipRoot(Type elementType)
    {
        QueryModelGuard.RequireAssignableTo(elementType, typeof(IRelationship), nameof(elementType));
        ElementType = elementType;
    }

    /// <summary>
    /// Gets the relationship element type.
    /// </summary>
    public Type ElementType { get; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Represents a node query root.
/// </summary>
public sealed record NodeRoot : QueryRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodeRoot"/> record.
    /// </summary>
    /// <param name="elementType">The node element type.</param>
    public NodeRoot(Type elementType)
    {
        QueryModelGuard.RequireAssignableTo(elementType, typeof(INode), nameof(elementType));
        ElementType = elementType;
    }

    /// <summary>
    /// Gets the node element type.
    /// </summary>
    public Type ElementType { get; }
}

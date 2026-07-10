// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Represents a dynamically typed query root.
/// </summary>
public sealed record DynamicRoot : QueryRoot
{
    /// <summary>
    /// Initializes a dynamically typed query root.
    /// </summary>
    /// <param name="elementType">The dynamic element type, when known.</param>
    public DynamicRoot(Type? elementType = null)
    {
        ElementType = elementType;
    }

    /// <summary>Gets the dynamic element type, when known.</summary>
    public Type? ElementType { get; }
}

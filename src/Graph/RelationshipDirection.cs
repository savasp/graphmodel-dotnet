// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines physical relationship orientation relative to an ordered pair of endpoint operands.
/// For a path segment, the operands are its start and end nodes.
/// </summary>
public enum RelationshipDirection
{
    /// <summary>
    /// The stored relationship points from the first endpoint to the second endpoint.
    /// </summary>
    Outgoing,

    /// <summary>
    /// The stored relationship points from the second endpoint to the first endpoint.
    /// </summary>
    Incoming
}

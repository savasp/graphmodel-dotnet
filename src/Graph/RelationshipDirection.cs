// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Defines the physical storage direction of a relationship relative to its start and end node IDs.
/// </summary>
public enum RelationshipDirection
{
    /// <summary>
    /// The stored relationship points from <c>StartNodeId</c> to <c>EndNodeId</c>.
    /// </summary>
    Outgoing,

    /// <summary>
    /// The stored relationship points from <c>EndNodeId</c> to <c>StartNodeId</c>.
    /// </summary>
    Incoming
}

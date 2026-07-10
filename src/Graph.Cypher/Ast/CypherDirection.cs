// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Describes the direction of a Cypher relationship pattern.
/// </summary>
public enum CypherDirection
{
    /// <summary>
    /// The relationship points from the preceding node to the following node.
    /// </summary>
    Outgoing,

    /// <summary>
    /// The relationship points from the following node to the preceding node.
    /// </summary>
    Incoming,

    /// <summary>
    /// The relationship may point in either direction.
    /// </summary>
    Both
}

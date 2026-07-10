// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>
/// Direction for graph traversal
/// </summary>
public enum GraphTraversalDirection
{
    /// <summary>Follow outgoing relationships</summary>
    Outgoing,

    /// <summary>Follow incoming relationships</summary>
    Incoming,

    /// <summary>Follow relationships in both directions</summary>
    Both,
}

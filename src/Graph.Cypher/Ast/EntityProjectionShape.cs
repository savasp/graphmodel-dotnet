// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Defines supported entity wire-projection shapes.</summary>
public enum EntityProjectionShape
{
    /// <summary>A single node projection.</summary>
    Node,

    /// <summary>A single relationship projection without endpoint nodes.</summary>
    Relationship,

    /// <summary>A start-node, relationship, and end-node path segment.</summary>
    PathSegment,
}

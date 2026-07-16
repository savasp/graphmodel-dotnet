// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Controls shortest-path selection for a path pattern.</summary>
public enum PathSelection
{
    /// <summary>Matches every path.</summary>
    All,

    /// <summary>Matches one shortest path.</summary>
    Shortest,

    /// <summary>Matches all equally short paths.</summary>
    AllShortest,
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Controls which paths a traversal returns for each source-target pair.</summary>
public enum TraversalPathSelection
{
    /// <summary>Returns every matching path.</summary>
    All,

    /// <summary>Returns one shortest matching path.</summary>
    Shortest,

    /// <summary>Returns every matching path tied for the shortest length.</summary>
    AllShortest,
}

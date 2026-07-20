// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Describes how a graph command consumes its selected elements.</summary>
public enum GraphElementSelectionMode
{
    /// <summary>Selects a deduplicated set of graph elements.</summary>
    Set,

    /// <summary>Probes for exactly one distinct graph element.</summary>
    ExactOne,
}

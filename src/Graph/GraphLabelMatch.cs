// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>Controls how multiple requested node labels are matched.</summary>
public enum GraphLabelMatch
{
    /// <summary>The node must have at least one requested label.</summary>
    Any,

    /// <summary>The node must have every requested label.</summary>
    All,
}

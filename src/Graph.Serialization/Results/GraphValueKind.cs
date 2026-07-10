// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.Results;

/// <summary>Identifies the shape of a provider-neutral graph wire value.</summary>
public enum GraphValueKind
{
    /// <summary>A scalar CLR value or null.</summary>
    Scalar,

    /// <summary>A graph node.</summary>
    Node,

    /// <summary>A graph relationship.</summary>
    Relationship,

    /// <summary>An alternating node/relationship graph path.</summary>
    Path,

    /// <summary>An ordered list of wire values.</summary>
    List,

    /// <summary>A string-keyed map of wire values.</summary>
    Map
}

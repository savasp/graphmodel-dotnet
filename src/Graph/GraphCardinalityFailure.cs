// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>Describes why an exact-one graph selection failed.</summary>
public enum GraphCardinalityFailure
{
    /// <summary>The selection produced no graph elements.</summary>
    Empty,

    /// <summary>The selection produced more than one distinct graph element.</summary>
    Multiple,
}

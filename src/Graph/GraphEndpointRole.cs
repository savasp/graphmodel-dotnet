// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

/// <summary>Identifies the endpoint whose graph selection failed cardinality validation.</summary>
public enum GraphEndpointRole
{
    /// <summary>The source endpoint.</summary>
    Source,

    /// <summary>The target endpoint.</summary>
    Target,
}

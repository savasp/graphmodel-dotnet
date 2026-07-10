// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Classifies the shape of a query projection.
/// </summary>
public enum ProjectionKind
{
    /// <summary>
    /// The projection returns the current element unchanged.
    /// </summary>
    Identity,

    /// <summary>
    /// The projection returns a scalar expression.
    /// </summary>
    Scalar,

    /// <summary>
    /// The projection returns an anonymous object shape.
    /// </summary>
    Anonymous,

    /// <summary>
    /// The projection returns a path segment or path-segment component.
    /// </summary>
    PathSegment,
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A constructor projection used to certify computed and null-propagating query results.</summary>
/// <param name="Name">The source name.</param>
/// <param name="OptionalText">The nullable source text.</param>
/// <param name="AdjustedScore">The computed score.</param>
/// <param name="Display">The null-propagating display value.</param>
public sealed record QueryExpressionCoverageProjection(
    string Name,
    string? OptionalText,
    int AdjustedScore,
    string Display);

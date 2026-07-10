// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents an inclusive relationship traversal depth range.
/// </summary>
public sealed record DepthRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthRange"/> class.
    /// </summary>
    /// <param name="min">The minimum traversal depth.</param>
    /// <param name="max">The maximum traversal depth.</param>
    /// <exception cref="ArgumentException">Thrown when the range is negative or inverted.</exception>
    public DepthRange(int min, int max)
    {
        if (min < 0)
        {
            throw new ArgumentException("The minimum depth cannot be negative.", nameof(min));
        }

        if (max < min)
        {
            throw new ArgumentException("The maximum depth cannot be less than the minimum depth.", nameof(max));
        }

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Gets the minimum traversal depth.
    /// </summary>
    public int Min { get; }

    /// <summary>
    /// Gets the maximum traversal depth.
    /// </summary>
    public int Max { get; }
}

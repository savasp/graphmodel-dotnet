// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes an inclusive traversal depth range.
/// </summary>
public sealed record DepthRange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DepthRange"/> record.
    /// </summary>
    /// <param name="min">The minimum traversal depth. Must be non-negative.</param>
    /// <param name="max">The maximum traversal depth. Must be greater than or equal to <paramref name="min"/>.</param>
    public DepthRange(int min, int max)
    {
        if (min < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum depth must be non-negative.");
        }

        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Maximum depth must be greater than or equal to minimum depth.");
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

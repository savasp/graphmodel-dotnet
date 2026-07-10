// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

internal sealed record GraphQueryModelBuilderOptions
{
    public const int DefaultMaxNodeCount = 10_000;
    public const int DefaultMaxDepth = 100;

    public GraphQueryModelBuilderOptions(
        int maxNodeCount = DefaultMaxNodeCount,
        int maxDepth = DefaultMaxDepth)
    {
        if (maxNodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNodeCount), "The maximum expression node count must be positive.");
        }

        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "The maximum expression depth must be positive.");
        }

        MaxNodeCount = maxNodeCount;
        MaxDepth = maxDepth;
    }

    public int MaxNodeCount { get; }

    public int MaxDepth { get; }
}

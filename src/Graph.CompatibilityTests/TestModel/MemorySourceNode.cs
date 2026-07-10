// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public record MemorySourceNode : Node
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }

    public required string Device { get; init; }
}
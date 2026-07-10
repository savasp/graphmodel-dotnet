// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Turn : Memory
{
    [Property(IsRequired = true)]
    public required string Speaker { get; init; }

    public string? Emotion { get; init; }
    public List<string>? Markers { get; init; }
}
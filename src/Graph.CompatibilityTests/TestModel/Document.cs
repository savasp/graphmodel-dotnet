// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Document : Memory
{
    [Property(IsRequired = true)]
    public required string Title { get; init; }

    [Property(IsRequired = true)]
    public required Content Content { get; init; }
}
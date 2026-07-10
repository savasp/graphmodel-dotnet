// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Link : Memory
{
    [Property(IsRequired = true)]
    public required Uri Url { get; init; }

    [Property(IsRequired = true)]
    public required string Note { get; init; }
}
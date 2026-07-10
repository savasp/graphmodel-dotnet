// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Node(Label = "SomeTask")]
public record SomeTask : Memory
{
    [Property(IsRequired = true)]
    public required string Title { get; init; }

    [Property(IsRequired = true)]
    public required string Description { get; init; }

    [Property(MinLength = 1, MaxLength = 100)]
    public string Priority { get; init; } = "Medium";
}

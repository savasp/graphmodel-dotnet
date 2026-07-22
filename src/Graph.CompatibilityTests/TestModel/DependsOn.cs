// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Relationship(Label = "DEPENDS_ON")]
public record DependsOn : Relationship
{
    [Property(IsRequired = true)]
    public string DependencyType { get; init; } = string.Empty;

    [Property(MinLength = 0, MaxLength = 100)]
    public string? Description { get; init; }
}

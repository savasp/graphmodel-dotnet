// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public record Conversation : Memory
{
    public List<string>? Topics { get; init; }
    public string? TimeOfDay { get; init; }
    public string? Weather { get; init; }
    public string? LocationDescription { get; init; }
}
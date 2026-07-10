// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

public record Content
{
    public required Uri Url { get; init; }
    public required string Base64 { get; init; }
    public required string Text { get; init; }
}
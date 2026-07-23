// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

namespace Documentation;

// snippet-start: root-model-knows
[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public DateTime Since { get; init; }
}
// snippet-end: root-model-knows

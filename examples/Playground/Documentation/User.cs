// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

namespace Documentation;

[Node(Label = "User")]
public record User : Node
{
    public bool IsActive { get; init; }

    public DateTime CreatedDate { get; init; }
}

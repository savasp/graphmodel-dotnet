// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Node(Label = "TodoWithoutRequired")]
public record TodoWithoutRequiredProperties : Memory
{
    [Property(Label = "note")]
    public string Note { get; init; } = string.Empty;

    [Property(Label = "done")]
    public bool Done { get; init; } = false;

    [Property(Label = "due")]
    public DateTime Due { get; init; } = DateTime.UtcNow;

    [Property(Label = "priority")]
    public Priority Priority { get; init; } = Priority.Normal;
}

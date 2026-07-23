// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

// snippet-start: graph-using
using Cvoya.Graph;
// snippet-end: graph-using

namespace Documentation;

// snippet-start: root-model-person
[Node(Label = "Person")]
public record Person : Node
{
    [Property(IsKey = true)]
    public string Tenant { get; init; } = string.Empty;

    [Property(IsKey = true)]
    public string Email { get; init; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public List<string?> Aliases { get; set; } = [];
    public List<Address?> PreviousAddresses { get; set; } = [];
}
// snippet-end: root-model-person

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

// ==== DOMAIN MODEL ====

[Node(Label = "Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? City { get; set; }
}


[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public Knows() : base(string.Empty, string.Empty) { }

    public Knows(string startNodeId, string endNodeId) : base(startNodeId, endNodeId)
    {
    }

    public DateTime Since { get; set; }
}
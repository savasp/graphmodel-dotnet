// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

// ==== DOMAIN MODEL ====

// snippet-start: person
[Node(Label = "Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
}
// snippet-end: person

[Node(Label = "Company")]
public record Company : Node
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime Founded { get; set; }
}

// snippet-start: works-for
[Relationship(Label = "WORKS_FOR")]
public record WorksFor : Relationship
{
    public string Position { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public decimal Salary { get; set; }
}
// snippet-end: works-for

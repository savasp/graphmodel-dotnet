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
}

[Node(Label = "Department")]
public record Department : Node
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

[Node(Label = "Company")]
public record Company : Node
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime Founded { get; set; }
}

[Relationship(Label = "WORKS_AT")]
public record WorksAt : Relationship
{
    public DateTime StartDate { get; set; }
    public decimal Salary { get; set; }
}

[Relationship(Label = "PART_OF")]
public record PartOf : Relationship
{
    public DateTime Since { get; set; }
}

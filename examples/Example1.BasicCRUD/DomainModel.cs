using Cvoya.Graph.Model;

// ==== DOMAIN MODEL ====
// Simple domain model for a basic organizational structure

[Node("Person")]
public class Person : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
}

[Node("Company")]
public class Company : Node
{
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime Founded { get; set; }
}

[Relationship("WORKS_FOR")]
public class WorksFor : Relationship<Person, Company>
{
    public string Position { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public decimal Salary { get; set; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Minimal test domain used by the LINQ-to-Cypher characterization suite. Deliberately
/// separate from the contract-suite domain model in Graph.Model.CompatibilityTests (that project
/// is an xunit test project, awkward to reference from here).
/// </summary>
[Node("Person")]
public record Person : Node
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public int Age { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateOnly HiredOn { get; set; }

    public TimeOnly ShiftStartsAt { get; set; }

    public TimeSpan Tenure { get; set; }

    public double Height { get; set; }

    public Address? HomeAddress { get; set; }

    [ComplexProperty(RelationshipType = "MAILING_ADDRESS")]
    public Address? MailingAddress { get; set; }

    public List<Address> Offices { get; set; } = [];

    public List<string> Nicknames { get; set; } = [];

    public EmploymentStatus Status { get; set; }
}

/// <summary>
/// A node type derived from <see cref="Person"/>, used to exercise inheritance-aware label
/// matching (compatible-labels union) in the visitor.
/// </summary>
[Node("Manager")]
public record Manager : Person
{
    public int DirectReportCount { get; set; }
}

[Node("Company")]
public record Company : Node
{
    public string Name { get; set; } = string.Empty;

    public string Industry { get; set; } = string.Empty;
}

/// <summary>
/// A complex (non-entity) property type embedded on <see cref="Person"/>.
/// </summary>
public record Address
{
    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public Region? Region { get; set; }
}

public record Region
{
    public string Name { get; set; } = string.Empty;
}

[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public int Since { get; set; }
}

[Relationship("WORKS_AT")]
public record WorksAt(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public string Title { get; set; } = string.Empty;

    public decimal Salary { get; set; }
}

public enum EmploymentStatus
{
    Active,
    OnLeave,
    Terminated
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests.Model;

/// <summary>
/// Minimal test domain used by the LINQ-to-Cypher characterization suite. Deliberately
/// separate from the contract-suite domain model in Graph.CompatibilityTests (that project
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

[Node("CustomPropertyLabelNode")]
public abstract record CustomPropertyLabelNode : Node
{
    [Property(Label = "last_name")]
    public string LastName { get; set; } = string.Empty;
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

/// <summary>
/// A node whose label is not a plain Cypher symbolic name, so pattern rendering must
/// backtick-escape it (#214). Abstract on purpose: <c>Labels.GetCompatibleLabels</c> unions every
/// concrete <see cref="INode"/> type in loaded assemblies, so a concrete type here would leak
/// this label into the INode-widened snapshots this suite pins.
/// </summary>
[Node("Label With Space")]
public abstract record SpacedLabelNode : Node
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A relationship whose type contains the alternation separator; the renderer must escape it as
/// one type name, not split it (#214).
/// </summary>
[Relationship("PIPE|SEPARATED")]
public record PipeTypedRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

/// <summary>
/// A relationship whose type contains a backtick, the escape character itself (#214).
/// </summary>
[Relationship("BACK`TICK")]
public record BacktickTypedRelationship(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

// Example domain models
public record Person : Node
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
    public string Bio { get; set; } = string.Empty;
    public Point Location { get; set; } = new Point { Longitude = 0.0, Latitude = 0.0, Height = 0.0 };
}

[Node("AtomicMutationNode")]
public record AtomicMutationNode : Node
{
    [Property(IsKey = true)]
    public string KeyGroup { get; set; } = string.Empty;

    [Property(IsKey = true)]
    public string KeyCode { get; set; } = string.Empty;

    [Property(IsUnique = true)]
    public string Email { get; set; } = string.Empty;

    public string Marker { get; set; } = string.Empty;
}

public record NullableCollectionCommandNode : Node
{
    [Property(IsUnique = true)]
    public string UniqueValue { get; set; } = string.Empty;

    public string Marker { get; set; } = string.Empty;

    public List<string?> Values { get; set; } = [];
}

[Node("AtomicOrdinaryIdNode")]
#pragma warning disable CG002, CG011 // This direct implementation verifies a domain property named Id.
public record AtomicOrdinaryIdNode : INode
{
    IReadOnlyList<string> INode.Labels => [];

    public IReadOnlyList<string> Labels { get; set; } = [];

    [Property(Label = "ordinary_id")]
    public string Id { get; set; } = string.Empty;

    public string Marker { get; set; } = string.Empty;
}
#pragma warning restore CG002, CG011

public record Manager : Person
{
    public string Department { get; set; } = string.Empty;
    public int TeamSize { get; set; }
}

// 3-level polymorphic node hierarchy for base/derived deserialization scenarios (see #136).
public record Animal : Node
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
}

public record Dog : Animal
{
    public string Breed { get; set; } = string.Empty;
}

public record PoliceDog : Dog
{
    public string Badge { get; set; } = string.Empty;
}

// 3-level polymorphic complex-property (POCO) hierarchy, mirroring Animal/Dog/PoliceDog, used to test
// whether a collection of base-typed complex properties preserves mixed derived instances (see #136).
public class AnimalDescription
{
    public string Name { get; set; } = string.Empty;
}

public class DogDescription : AnimalDescription
{
    public string Breed { get; set; } = string.Empty;
}

public class PoliceDogDescription : DogDescription
{
    public string Badge { get; set; } = string.Empty;
    public HandlerDescription? Handler { get; set; }
}

// Nested complex property used to verify that complex properties on a *derived* collection
// element also round-trip correctly (see #146).
public class HandlerDescription
{
    public string Name { get; set; } = string.Empty;
}

public record Kennel : Node
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;
    public List<AnimalDescription> Animals { get; set; } = new();
}

public record Address : Node
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class AddressValue
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

// A label that is not a plain Cypher symbolic name: providers must escape it on the write path
// AND in query MATCH patterns, so create-then-query round-trips (#214).
[Node("Escapable Label")]
public record SpacedLabelVenue : Node
{
    public string Name { get; set; } = string.Empty;
}

// Complex property with a nullable leaf, for pinning the #221 null-vs-missing navigation
// semantics: "no Profile node" and "Profile with null Motto" both satisfy Motto == null.
public class OptionalProfileValue
{
    public string? Motto { get; set; }
}

public record PersonWithOptionalProfile : Node
{
    public string FirstName { get; set; } = string.Empty;
    public OptionalProfileValue? Profile { get; set; }
}

[Relationship(Label = "FRIENDOF")]
public record Friend : Relationship
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime Since { get; set; } = DateTime.UtcNow;
}

[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime Since { get; set; } = DateTime.UtcNow;
}

[Relationship(Label = "ATOMIC_MUTATION_RELATIONSHIP")]
public record AtomicMutationRelationship : Relationship
{
    [Property(IsUnique = true)]
    public string Code { get; set; } = string.Empty;

    public string Marker { get; set; } = string.Empty;
}

[Relationship(Label = "WORKS_REALLY_WELL_WITH")]
public record KnowsWell : Knows
{
    public string HowWell { get; set; } = string.Empty;
}

[Relationship(Label = "LIVES_AT")]
public record LivesAt : Relationship
{
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime MovedInDate { get; set; } = DateTime.UtcNow;
}

public record PersonWithComplexProperty : Person
{
    public AddressValue Address { get; set; } = new AddressValue();
}

public record PersonWithComplexProperties : Node
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Bio { get; set; } = string.Empty;
    public AddressValue Address { get; set; } = new AddressValue();
    // TODO: Add serialization support for dictionaries.
    //public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class CommandContactValue
{
    public string Name { get; set; } = string.Empty;

    public AddressValue? Address { get; set; }
}

public record ComplexCommandNode : Node
{
    public string Group { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    [ComplexProperty(RelationshipType = "COMMAND_CONTACT")]
    public CommandContactValue? Contact { get; set; }

    public List<AnimalDescription> Animals { get; set; } = [];
}

public record Class1 : Node
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
    public ComplexClassA? A { get; set; }
    public ComplexClassB? B { get; set; }
}

public record Class2 : Node
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
    public List<ComplexClassA> A { get; set; } = new List<ComplexClassA>();
    public List<ComplexClassB> B { get; set; } = new List<ComplexClassB>();
}
public class ComplexClassA
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
    public ComplexClassB? B { get; set; }
    public ComplexClassC? C { get; set; }
}

public class ComplexClassB
{
    public string Property1 { get; set; } = string.Empty;
    public ComplexClassA? A { get; set; }
}

public class ComplexClassC
{
    public string Property1 { get; set; } = string.Empty;
    public ComplexClassB? B { get; set; }
}

public class ComplexClassD
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
}

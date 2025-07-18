// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cvoya.Graph.Model.Tests;

// Example domain models
public class Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
    public string Bio { get; set; } = string.Empty;
    public Point Location { get; set; } = new Point { Longitude = 0.0, Latitude = 0.0, Height = 0.0 };
}

public class Manager : Person
{
    public string Department { get; set; } = string.Empty;
    public int TeamSize { get; set; } = 0;
}

public class Address : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class AddressValue
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

[Relationship(Label = "FRIENDOF")]
public class Friend : IRelationship
{
    public Friend() { }
    public Friend(string startNodeId, string endNodeId)
    {
        this.StartNodeId = startNodeId;
        this.EndNodeId = endNodeId;
    }
    public Friend(INode source, INode target)
    {
        this.StartNodeId = source.Id;
        this.EndNodeId = target.Id;
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public DateTime Since { get; set; } = DateTime.UtcNow;
}

[Relationship(Label = "KNOWS")]
public class Knows : IRelationship
{
    public Knows() { }
    public Knows(string startNodeId, string endNodeId)
    {
        this.StartNodeId = startNodeId;
        this.EndNodeId = endNodeId;
    }
    public Knows(INode source, INode target)
    {
        this.StartNodeId = source.Id;
        this.EndNodeId = target.Id;
    }
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public DateTime Since { get; set; }
}

[Relationship(Label = "WORKS_REALLY_WELL_WITH")]
public class KnowsWell : Knows
{
    public KnowsWell() { }
    public KnowsWell(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    public KnowsWell(INode source, INode target) : base(source, target) { }
    public string HowWell { get; set; } = string.Empty;
}

[Relationship(Label = "LIVES_AT")]
public class LivesAt : IRelationship
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public DateTime MovedInDate { get; set; } = DateTime.UtcNow;
}

public class PersonWithComplexProperty : Person
{
    public AddressValue Address { get; set; } = new AddressValue();
}

public class PersonWithComplexProperties : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
    public string Bio { get; set; } = string.Empty;
    public AddressValue Address { get; set; } = new AddressValue();
    // TODO: Add serialization support for dictionaries.
    //public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public record Class1 : Node
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
    public ComplexClassA? A { get; set; } = null;
    public ComplexClassB? B { get; set; } = null;
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
    public ComplexClassB? B { get; set; } = null;
    public ComplexClassC? C { get; set; } = null;
}

public class ComplexClassB
{
    public string Property1 { get; set; } = string.Empty;
    public ComplexClassA? A { get; set; } = null;
}

public class ComplexClassC
{
    public string Property1 { get; set; } = string.Empty;
    public ComplexClassB? B { get; set; } = null;
}

public class ComplexClassD
{
    public string Property1 { get; set; } = string.Empty;
    public string Property2 { get; set; } = string.Empty;
}

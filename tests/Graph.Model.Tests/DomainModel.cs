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

[Relationship(Label = "KNOWS")]
public class Knows : IRelationship
{
    public Knows() { }
    public Knows(string sourceId, string targetId)
    {
        this.StartNodeId = sourceId;
        this.EndNodeId = targetId;
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
    public KnowsWell(string sourceId, string targetId) : base(sourceId, targetId) { }
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
    public Address Address { get; set; } = new Address();
}

public class PersonWithComplexProperties : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
    public string Bio { get; set; } = string.Empty;
    public Address Address { get; set; } = new Address();
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public record KnowsWithComplexProperty(PersonWithComplexProperty p1, PersonWithComplexProperty p2) : Relationship(p1.Id, p2.Id)
{
    public DateTime Since { get; set; }
    public Address MetAt { get; set; } = new Address();
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

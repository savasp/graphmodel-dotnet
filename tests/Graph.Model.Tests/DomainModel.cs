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
public record Person : Node
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
    public string Bio { get; set; } = string.Empty;
    public Point Location { get; set; } = new Point { Longitude = 0.0, Latitude = 0.0, Height = 0.0 };
}

public record Manager : Person
{
    public string Department { get; set; } = string.Empty;
    public int TeamSize { get; set; } = 0;
}

public record Address : Node
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class AddressValue
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

[Relationship(Label = "FRIENDOF")]
public record Friend : Relationship
{
    public Friend() : base(string.Empty, string.Empty) { }

    public Friend(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

    public Friend(INode source, INode target) : base(source.Id, target.Id) { }

    public DateTime Since { get; set; } = DateTime.UtcNow;
}

[Relationship(Label = "KNOWS")]
public record Knows : Relationship
{
    public Knows() : base(string.Empty, string.Empty) { }
    public Knows(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

    public Knows(INode source, INode target) : base(source.Id, target.Id) { }

    public DateTime Since { get; set; } = DateTime.UtcNow;
}

[Relationship(Label = "WORKS_REALLY_WELL_WITH")]
public record KnowsWell : Knows
{
    public KnowsWell() { }
    public KnowsWell(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    public KnowsWell(INode source, INode target) : base(source, target) { }
    public string HowWell { get; set; } = string.Empty;
}

[Relationship(Label = "LIVES_AT")]
public record LivesAt : Relationship
{
    public LivesAt() : base(string.Empty, string.Empty) { }
    public LivesAt(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }
    public LivesAt(INode source, INode target) : base(source.Id, target.Id) { }
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



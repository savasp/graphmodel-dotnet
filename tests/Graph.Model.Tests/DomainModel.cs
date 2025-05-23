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
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; } = 30;
    public DateTime DateOfBirth { get; set; } = DateTime.UtcNow;
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

[Relationship("KNOWS")]
public class Knows<T, S> : Relationship<T, S>
    where T : Person
    where S : Person
{
    public Knows() { }
    public Knows(T source, S target) : base(source, target)
    {
    }
    public DateTime Since { get; set; }
}

public class PersonWithNavigationProperty : Person
{
    public List<Knows<PersonWithNavigationProperty, PersonWithNavigationProperty>> Knows { get; set; } = new();
}

public class PersonWithComplexProperty : Person
{
    public Address Address { get; set; } = new Address();
}

public class KnowsWithComplexProperty : Relationship<PersonWithComplexProperty, PersonWithComplexProperty>
{
    public KnowsWithComplexProperty() { }
    public KnowsWithComplexProperty(PersonWithComplexProperty p1, PersonWithComplexProperty p2) : base(p1, p2) { }
    public DateTime Since { get; set; }
    public Address MetAt { get; set; } = new Address();
}

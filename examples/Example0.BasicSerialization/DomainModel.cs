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

using Cvoya.Graph.Model;

// ==== DOMAIN MODEL ====

public enum State
{
    WA,
    CA,
    OR,
    NY,
    Unknown,
}

public record Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public State State { get; set; } = State.Unknown;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Note { get; set; } = null;
    public List<string> Aliases { get; set; } = new List<string>();
}

public enum EmotionalState
{
    Happy,
    Sad,
    Angry,
    Excited,
    Bored,
    Anxious,
    Relaxed,
    Confused,
    Curious,
    Frustrated
}

[Node(Label = "Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
    public List<string> Skills { get; set; } = new List<string>();
    public List<DateTime> KeyDates { get; set; } = new List<DateTime>();
    public List<int> SomeNumbers { get; set; } = new List<int>();
    public List<EmotionalState> EmotionalStates { get; set; } = new List<EmotionalState>();
}

public record Foo
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<DateTime> ImportantDates { get; set; } = new List<DateTime>();
    public Bar Bar { get; set; } = new Bar();
}

public record Bar
{
    public string Description { get; set; } = string.Empty;
    public List<int> Numbers { get; set; } = new List<int>();
    public Foo? Foo { get; set; } = null;
    public Baz? Baz { get; set; } = null;
}

public record Baz
{
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Bar? Bar { get; set; } = null;
}

[Node(Label = "PersonWithAddress")]
public record PersonWithComplex : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
    public Address HomeAddress { get; set; } = new Address();
    public Address? WorkAddress { get; set; } = null;
    public List<Address> PreviousAddresses { get; set; } = new List<Address>();
    public Foo Foo { get; set; } = new Foo();
    public Bar? Bar { get; set; } = null;
}

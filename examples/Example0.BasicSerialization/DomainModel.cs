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

[Node(Label = "PersonWithAddress")]
public record PersonWithAddress : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
    public Address HomeAddress { get; set; } = new Address();
    public Address? WorkAddress { get; set; } = null;
}

[Node(Label = "PersonWithListOfAddresses")]
public record PersonWithListOfAddresses : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
    public List<Address> Addresses { get; set; } = new List<Address>();
}
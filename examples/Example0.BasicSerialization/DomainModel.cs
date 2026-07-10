// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

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
    public City City { get; set; } = new City();
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

public class Foo
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<DateTime> ImportantDates { get; set; } = new List<DateTime>();
    public Bar Bar { get; set; } = new Bar();
}

public class Bar
{
    public string Description { get; set; } = string.Empty;
    public List<int> Numbers { get; set; } = new List<int>();
    public Foo? Foo { get; set; } = null;
    public Baz? Baz { get; set; } = null;
}

public class Baz
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

public class City
{
    public string Name { get; set; } = string.Empty;
}

[Relationship(Label = "FRIEND_OF")]
public record Friend : Relationship
{
    public Friend() : base(string.Empty, string.Empty)
    {
    }

    public Friend(string startNodeId, string endNodeId) : base(startNodeId, endNodeId)
    {
    }
    public DateTime Since { get; set; }
}
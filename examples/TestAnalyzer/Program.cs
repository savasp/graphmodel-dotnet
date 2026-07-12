// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace TestAnalyzer;

using Cvoya.Graph;


// Very simple test - should work
public record SimpleNode : Node
{
}

// Test case similar to failing CG004 test
public class CustomClass
{
    public string Value { get; set; } = string.Empty;
}

// Test case with explicit constructor for CG005
public class ValidComplexType
{
    public ValidComplexType() { }
    public string Value { get; set; } = string.Empty;
}

public record TestNode : Node
{
    public CustomClass Custom { get; set; } = new CustomClass(); // This should produce CG004
    public ValidComplexType Valid { get; set; } = new ValidComplexType(); // This should be OK
}

// Bug reproduction tests
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Foo
{
    public string Name { get; set; } = string.Empty;
}

public class Bar
{
    public string Value { get; set; } = string.Empty;
}

// Test Bug 1: Record with Address should not report circular reference
[Node(Label = "PersonWithAddress")]
public record PersonWithComplex : Node
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
    public Address HomeAddress { get; set; } = new Address();
    public Address? WorkAddress { get; set; }
    public List<Address> PreviousAddresses { get; set; } = new List<Address>();
    public Foo Foo { get; set; } = new Foo();
    public Bar? Bar { get; set; }
}

public class Program
{
    public static void Main()
    {
        Console.WriteLine("This project tests the analyzer. Check build output for diagnostics.");
    }
}
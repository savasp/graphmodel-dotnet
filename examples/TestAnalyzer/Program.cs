using Cvoya.Graph.Model;

namespace TestAnalyzer;

// Very simple test - should work
public class SimpleNode : INode
{
    public SimpleNode() { }
    public string Id { get; init; } = string.Empty;
}

// Test case similar to failing GM004 test
public class CustomClass
{
    public string Value { get; set; } = string.Empty;
}

// Test case with explicit constructor for GM005
public class ValidComplexType
{
    public ValidComplexType() { }
    public string Value { get; set; } = string.Empty;
}

public class TestNode : INode
{
    public string Id { get; init; } = string.Empty;
    public CustomClass Custom { get; set; } = new CustomClass(); // This should produce GM004
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
    public Address? WorkAddress { get; set; } = null;
    public List<Address> PreviousAddresses { get; set; } = new List<Address>();
    public Foo Foo { get; set; } = new Foo();
    public Bar? Bar { get; set; } = null;
}

public class Program
{
    public static void Main()
    {
        Console.WriteLine("This project tests the analyzer. Check build output for diagnostics.");
    }
}
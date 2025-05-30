using Cvoya.Graph.Model;

namespace TestAnalyzer;

// Very simple test - should work
public class SimpleNode : INode
{
    public SimpleNode() { }
    public string Id { get; set; } = string.Empty;
}

// Test case similar to failing GM004 test
public class CustomClass
{
    public string Value { get; set; }
}

// Test case with explicit constructor for GM005
public class ValidComplexType
{
    public ValidComplexType() { }
    public string Value { get; set; }
}

public class TestNode : INode
{
    public string Id { get; set; } = string.Empty;
    public CustomClass Custom { get; set; } // This should produce GM004
    public ValidComplexType Valid { get; set; } // This should be OK
}

public class Program
{
    public static void Main()
    {
        Console.WriteLine("This project tests the analyzer. Check build output for diagnostics.");
    }
}
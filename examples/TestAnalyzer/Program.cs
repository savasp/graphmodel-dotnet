using Cvoya.Graph.Model;

namespace TestAnalyzer;

// Very simple test - should work
public class SimpleNode : INode
{
    public SimpleNode() { }
    public string Id { get; set; } = string.Empty;
}

public class Program
{
    public static void Main()
    {
        Console.WriteLine("This project tests the analyzer. Check build output for diagnostics.");
    }
}
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

namespace TestAnalyzer;

using Cvoya.Graph.Model;


// Very simple test - should work
public record SimpleNode : Node
{
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

public record TestNode : Node
{
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
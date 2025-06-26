# Graph.Model.Analyzers

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Analyzers.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

Compile-time code analyzers for Graph.Model that ensure proper implementation of `INode` and `IRelationship` interfaces, helping catch common errors during development.

## 🌟 Overview

Graph.Model.Analyzers provides Roslyn-based code analyzers that validate your graph domain model at compile time. These analyzers help prevent runtime errors by catching common mistakes in node and relationship definitions before your code is deployed.

## 🚀 Features

- **🔍 Compile-Time Validation** - Catch errors before runtime
- **🛡️ Interface Compliance** - Ensure proper `INode` and `IRelationship` implementation
- **⚡ Property Validation** - Verify property types and accessibility
- **🎯 Attribute Validation** - Check proper usage of `[Node]`, `[Relationship]`, and `[Property]` attributes
- **🔧 Constructor Requirements** - Enforce parameterless constructor availability
- **♻️ Circular Reference Detection** - Prevent infinite loops in complex properties
- **💡 Helpful Diagnostics** - Clear error messages with suggested fixes

## 📦 Installation

```bash
dotnet add package Cvoya.Graph.Model.Analyzers
```

The analyzers are automatically included when you build your project. No additional configuration is required.

## 🔧 Analyzer Rules

### GM001: Missing Parameterless Constructor

**Problem**: Classes implementing `INode` or `IRelationship` must have a parameterless constructor.

```csharp
// ❌ This will trigger GM001
[Node("Person")]
public class Person : INode
{
    public Person(string name) { ... } // Only constructor with parameters
    public string Id { get; set; }
    public string Name { get; set; }
}

// ✅ Correct implementation
[Node("Person")]
public class Person : INode
{
    public Person() { } // Parameterless constructor
    public Person(string name) : this() { Name = name; }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}
```

### GM002: Property Must Have Public Accessors

**Problem**: Properties used in graph entities must have public getters and setters.

```csharp
// ❌ This will trigger GM002
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    private string Name { get; set; } // Private property
    public string Email { get; private set; } // Private setter
}

// ✅ Correct implementation
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; } // Public getter and setter
    public string Email { get; set; } // Public getter and setter
}
```

### GM003: Property Cannot Be Graph Interface Type

**Problem**: Properties cannot be of type `INode`, `IRelationship`, or their generic variants.

```csharp
// ❌ This will trigger GM003
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public INode Friend { get; set; } // Cannot use interface type
    public List<IRelationship> Relationships { get; set; } // Cannot use interface type
}

// ✅ Correct implementation
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public Person Friend { get; set; } // Use concrete type
    public List<Knows> Relationships { get; set; } // Use concrete relationship type
}
```

### GM004: Invalid Property Type for Node

**Problem**: Node properties must be of supported types (primitives, enums, complex objects, collections).

```csharp
// ❌ This will trigger GM004
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public Task<string> AsyncProperty { get; set; } // Unsupported type
    public Action Callback { get; set; } // Unsupported type
}

// ✅ Correct implementation
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; } // Primitive type
    public Address HomeAddress { get; set; } // Complex object
    public List<string> Skills { get; set; } // Collection
}
```

### GM005: Invalid Property Type for Relationship

**Problem**: Relationship properties must be of supported types, with additional restrictions.

```csharp
// ❌ This will trigger GM005
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public RelationshipDirection Direction { get; init; }

    public Person Source { get; set; }
    public Person Target { get; set; }

    public Stream DataStream { get; set; } // Unsupported type
}

// ✅ Correct implementation
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public RelationshipDirection Direction { get; init; }

    public Person Source { get; set; }
    public Person Target { get; set; }

    public DateTime Since { get; set; } // Supported type
    public int Strength { get; set; } // Supported type
}
```

### GM006: Complex Type Contains Graph Interface Types

**Problem**: Complex properties cannot contain graph interface types anywhere in their object graph.

```csharp
// ❌ This will trigger GM006
public class Address
{
    public string Street { get; set; }
    public INode ClosestLandmark { get; set; } // Cannot contain graph interface
}

[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public Address HomeAddress { get; set; } // This address type is invalid
}

// ✅ Correct implementation
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public Address HomeAddress { get; set; } // Valid complex type
}
```

### GM007: Duplicate Property Attribute Label

**Problem**: Multiple properties cannot have the same custom label within the same entity.

```csharp
// ❌ This will trigger GM007
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }

    [Property("name")]
    public string FirstName { get; set; }

    [Property("name")] // Duplicate label
    public string LastName { get; set; }
}

// ✅ Correct implementation
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }

    [Property("first_name")]
    public string FirstName { get; set; }

    [Property("last_name")]
    public string LastName { get; set; }
}
```

### GM008: Duplicate Relationship Attribute Label

**Problem**: Multiple relationship types cannot have the same label.

```csharp
// ❌ This will trigger GM008
[Relationship("KNOWS")]
public class Friendship : IRelationship<Person, Person> { ... }

[Relationship("KNOWS")] // Duplicate relationship label
public class Acquaintance : IRelationship<Person, Person> { ... }

// ✅ Correct implementation
[Relationship("FRIENDSHIP")]
public class Friendship : IRelationship<Person, Person> { ... }

[Relationship("ACQUAINTANCE")]
public class Acquaintance : IRelationship<Person, Person> { ... }
```

### GM009: Duplicate Node Attribute Label

**Problem**: Multiple node types cannot have the same primary label.

```csharp
// ❌ This will trigger GM009
[Node("Person")]
public class Employee : INode { ... }

[Node("Person")] // Duplicate node label
public class Customer : INode { ... }

// ✅ Correct implementation
[Node("Employee", "Person")]
public class Employee : INode { ... }

[Node("Customer", "Person")]
public class Customer : INode { ... }
```

### GM010: Circular Reference Without Nullable

**Problem**: Complex properties with circular references must use nullable types to prevent infinite loops.

```csharp
// ❌ This will trigger GM010
public class Person : INode
{
    public string Id { get; set; }
    public Person Manager { get; set; } // Should be nullable
}

public class Department
{
    public List<Person> Employees { get; set; }
    public Department ParentDepartment { get; set; } // Should be nullable
}

// ✅ Correct implementation
public class Person : INode
{
    public string Id { get; set; }
    public Person? Manager { get; set; } // Nullable prevents infinite loops
}

public class Department
{
    public List<Person> Employees { get; set; }
    public Department? ParentDepartment { get; set; } // Nullable
}
```

## 🎯 Integration

### MSBuild Integration

The analyzers automatically integrate with MSBuild and will run during compilation:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <PackageReference Include="Cvoya.Graph.Model" Version="1.0.0" />
  <PackageReference Include="Cvoya.Graph.Model.Analyzers" Version="1.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</Project>
```

### IDE Support

The analyzers work with all major IDEs:

- **Visual Studio**: Errors appear in Error List and with red squigglies
- **Visual Studio Code**: Integration via C# extension
- **JetBrains Rider**: Native Roslyn analyzer support
- **Visual Studio for Mac**: Full analyzer support

### CI/CD Integration

Analyzer violations will cause build failures in CI/CD pipelines:

```bash
# This will fail if analyzer rules are violated
dotnet build --configuration Release --no-restore
```

## ⚙️ Configuration

### Disabling Specific Rules

You can disable specific analyzer rules in your project:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);GM001;GM002</NoWarn>
</PropertyGroup>
```

### Rule Severity

Configure rule severity levels:

```xml
<PropertyGroup>
  <WarningsAsErrors />
  <WarningsNotAsErrors>GM010</WarningsNotAsErrors>
</PropertyGroup>
```

### EditorConfig Support

Configure rules via `.editorconfig`:

```ini
[*.cs]
# Make GM001 a warning instead of error
dotnet_diagnostic.GM001.severity = warning

# Disable GM010 completely
dotnet_diagnostic.GM010.severity = none
```

## 🧪 Testing

The analyzers include comprehensive tests to ensure reliability:

```csharp
[Fact]
public async Task GM001_MissingParameterlessConstructor_ShouldTriggerDiagnostic()
{
    var test = @"
        using Cvoya.Graph.Model;

        [Node(""Person"")]
        public class Person : INode
        {
            public Person(string name) { }
            public string Id { get; set; }
        }";

    var expected = new DiagnosticResult
    {
        Id = "GM001",
        Message = "Class 'Person' implementing INode must have a parameterless constructor",
        Severity = DiagnosticSeverity.Error,
        Locations = new[] { new DiagnosticResultLocation("Test0.cs", 4, 22) }
    };

    await VerifyCSharpDiagnosticAsync(test, expected);
}
```

## 🚀 Development

### Building

```bash
dotnet build src/Graph.Model.Analyzers
```

### Running Tests

```bash
dotnet test src/Graph.Model.Analyzers.Tests
```

### Adding New Rules

1. Create a new diagnostic descriptor in `DiagnosticDescriptors.cs`
2. Implement the analyzer in `GraphModelAnalyzer.cs`
3. Add comprehensive tests in the test project
4. Update documentation

## 📚 Documentation

- **[Core Interfaces](../Graph.Model/docs/core-interfaces.md)** - Understanding the Graph Model type system
- **[Best Practices](../Graph.Model/docs/best-practices.md)** - Guidelines for proper implementation
- **[Getting Started](../Graph.Model/docs/getting-started.md)** - Basic usage examples

## 🔧 Requirements

- **.NET 8.0** or later
- **C# 12** language features
- **Roslyn 4.0+** for analyzer infrastructure

## 📄 License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.

---

**Built with ❤️ to help you write better graph code**

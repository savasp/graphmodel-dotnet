# Graph.Model.Analyzers

Code analyzers for the Graph.Model library that ensure proper implementation of `INode` and `IRelationship` interfaces, helping you catch common implementation errors at compile time.

## Overview

The Graph.Model.Analyzers package provides compile-time validation for classes implementing `INode` and `IRelationship` interfaces. These analyzers enforce the constraints required by graph providers and help prevent runtime errors by catching issues during development.

## Features

- **Compile-time validation** - Catch implementation errors before runtime
- **Comprehensive rule coverage** - Validates class structure, constructors, and properties
- **Clear error messages** - Detailed diagnostics with actionable guidance
- **IDE integration** - Works seamlessly with Visual Studio, VS Code, and other IDEs
- **Zero runtime overhead** - Analysis happens only during compilation

## Installation

```bash
dotnet add package Cvoya.Graph.Model.Analyzers
```

The analyzers are automatically enabled when you install the package and will run during compilation.

## Diagnostic Rules

### GM001: Only classes can implement INode or IRelationship

**Error**: Structs cannot implement `INode` or `IRelationship` interfaces.

**Reason**: Graph providers require reference types for proper entity tracking and relationship management.

**Example Violation**:
```csharp
// ❌ This will trigger GM001
public struct PersonStruct : INode  
{
    public string Id { get; set; }
}
```

**Fix**:
```csharp
// ✅ Use a class instead
public class Person : INode  
{
    public string Id { get; set; }
}
```

### GM002: Missing parameterless constructor

**Error**: Types implementing `INode` or `IRelationship` must have a parameterless constructor.

**Reason**: Graph providers need to instantiate entities during deserialization and query operations.

**Example Violation**:
```csharp
// ❌ This will trigger GM002
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; }
    
    public Person(string name) // Only constructor with parameters
    {
        Name = name;
        Id = Guid.NewGuid().ToString();
    }
}
```

**Fix**:
```csharp
// ✅ Add a parameterless constructor
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    
    public Person() { } // Parameterless constructor
    
    public Person(string name) : this()
    {
        Name = name;
    }
}
```

### GM003: Property must have public getter and setter

**Error**: All properties in `INode` and `IRelationship` implementations must have public getters and setters.

**Reason**: Graph providers need to read and write all properties during serialization and deserialization operations.

**Example Violation**:
```csharp
// ❌ This will trigger GM003
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; private set; } // Private setter
    private string Email { get; set; }       // Private property
}
```

**Fix**:
```csharp
// ✅ Make all properties publicly accessible
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// ✅ Or use [Property(Ignore = true)] for computed properties
public class Person : INode
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    [Property(Ignore = true)]
    public string FullName => $"{FirstName} {LastName}"; // Read-only computed property
}
```

### GM004: Unsupported property type

**Error**: Properties can only be of supported types.

**Reason**: Graph databases have limitations on the types they can store directly. Complex types need special handling.

**Supported Types**:
- Primitive types (`int`, `long`, `double`, `bool`, etc.)
- `string`
- Date/time types (`DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`)
- `Point` (spatial data)
- Collections of supported types (`List<T>`, `T[]`, etc.)
- Nullable versions of supported types
- Valid complex types (for `INode` only - see GM005)

**Example Violation**:
```csharp
// ❌ This will trigger GM004
public class Person : INode
{
    public string Id { get; set; }
    public Dictionary<string, object> Metadata { get; set; } // Unsupported type
    public Stream ProfileImage { get; set; }                // Unsupported type
}
```

**Fix**:
```csharp
// ✅ Use supported types or ignore unsupported properties
public class Person : INode
{
    public string Id { get; set; }
    public string MetadataJson { get; set; }  // Store as JSON string
    
    [Property(Ignore = true)]
    public Dictionary<string, object> Metadata // Computed property
    {
        get => JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson);
        set => MetadataJson = JsonSerializer.Serialize(value);
    }
    
    [Property(Ignore = true)]
    public Stream ProfileImage { get; set; } // Not persisted to graph
}
```

### GM005: Invalid complex type property

**Error**: Complex type properties in `INode` implementations must be classes with parameterless constructors and only simple properties.

**Reason**: Graph providers serialize complex types as separate entities or property relationships, requiring specific constraints.

**Valid Complex Types for INode**:
- Must be a class (not struct)
- Must have a parameterless constructor
- All properties must be of simple supported types
- Cannot have nested complex types

**Example Violation**:
```csharp
// ❌ This will trigger GM005
public class Person : INode
{
    public string Id { get; set; }
    public Address Address { get; set; } // Invalid complex type
}

public class Address
{
    public string Street { get; private set; } // Private setter - invalid
    public Location Location { get; set; }     // Nested complex type - invalid
    
    public Address(string street) { Street = street; } // No parameterless constructor
}
```

**Fix**:
```csharp
// ✅ Make complex type conform to requirements
public class Person : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }    // Public getter/setter
    public double Latitude { get; set; }  // Simple types only
    public double Longitude { get; set; }
    
    public Address() { } // Parameterless constructor
    
    public Address(string street, double lat, double lng) : this()
    {
        Street = street;
        Latitude = lat;
        Longitude = lng;
    }
}
```

## Usage

The analyzers run automatically during compilation. You don't need to configure anything - just install the package and build your project.

### IDE Integration

- **Visual Studio**: Violations appear as red squiggles with error messages
- **VS Code**: Integrated with C# extension for real-time feedback
- **Command Line**: Errors appear in build output with rule IDs

### Suppressing Warnings

If you need to suppress specific analyzer rules (not recommended), you can use:

```csharp
#pragma warning disable GM001 // Suppress specific rule
public struct SpecialCase : INode { /* ... */ }
#pragma warning restore GM001
```

Or in your project file:
```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);GM001;GM002</NoWarn>
</PropertyGroup>
```

## Best Practices

1. **Always use classes** for `INode` and `IRelationship` implementations
2. **Provide parameterless constructors** - they can be public or internal
3. **Use public properties** with both getters and setters
4. **Leverage `[Property(Ignore = true)]`** for computed or non-persisted properties
5. **Keep complex types simple** - avoid deep nesting and circular references
6. **Consider JSON serialization** for complex data that doesn't need querying

## Requirements

- .NET 10.0 or later
- Graph.Model package

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.
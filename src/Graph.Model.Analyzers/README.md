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

### GM001: Missing parameterless constructor or property-initializing constructor

**Error**: Types implementing `INode` or `IRelationship` must have a parameterless constructor or a constructor that initializes all properties.

**Reason**: Graph providers need to instantiate entities during deserialization and query operations, either through parameterless constructors or constructors that properly initialize all necessary properties.

**Example Violation**:
```csharp
// ❌ This will trigger GM001
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public Person(string name, string id, string extraParam) // Only constructor with no property initialization guarantee
    {
        // Properties might not be fully initialized
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

// ✅ Or ensure constructor initializes properties
public struct PersonStruct : INode // Structs are now allowed!
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public PersonStruct(string name)
    {
        Id = Guid.NewGuid().ToString();
        Name = name;
    }
}
```

### GM002: Property must have public getter and setter or initializer

**Error**: Properties in `INode` and `IRelationship` implementations must have public getters and either public setters or public initializers.

**Reason**: Graph providers need to read and write properties during serialization and deserialization operations. Public initializers (`init`) are now supported as an alternative to setters.

**Example Violation**:
```csharp
// ❌ This will trigger GM002
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; private set; } // Private setter without init
    private string Email { get; set; }       // Private property
}
```

**Fix**:
```csharp
// ✅ Use public setters
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// ✅ Or use public initializers
public class Person : INode
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
}
```

### GM003: Property cannot be INode or IRelationship

**Error**: Properties of types implementing `INode` or `IRelationship` cannot be `INode` or `IRelationship` types or collections of them.

**Reason**: Graph databases model relationships explicitly, not as properties containing other nodes or relationships. This prevents circular references and ensures proper relationship management.

**Example Violation**:
```csharp
// ❌ This will trigger GM003
public class Person : INode
{
    public string Id { get; set; }
    public Person? Parent { get; set; }           // Another INode
    public List<Person> Children { get; set; }    // Collection of INode
    public FrienshipRel[] Friends { get; set; }   // Array of IRelationship
}

public class FrienshipRel : IRelationship
{
    public string Id { get; set; }
}
```

**Fix**:
```csharp
// ✅ Use simple properties or complex types
public class Person : INode
{
    public string Id { get; set; }
    public string ParentId { get; set; }         // Reference by ID
    public List<string> ChildrenIds { get; set; } // Collection of IDs
    public Address Address { get; set; }          // Complex type (allowed)
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}
```

### GM004: Complex property contains invalid nested properties

**Error**: Properties of complex types used in `INode` implementations cannot contain `INode` or `IRelationship` types or collections of them (applied recursively).

**Reason**: This rule ensures that complex types don't indirectly introduce node or relationship references through their nested properties.

**Example Violation**:
```csharp
// ❌ This will trigger GM004
public class Person : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; } // Complex type with invalid nested properties
}

public class ContactInfo
{
    public string Email { get; set; }
    public Person EmergencyContact { get; set; } // INode in nested property - invalid!
}
```

**Fix**:
```csharp
// ✅ Keep complex types simple
public class Person : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public string EmergencyContactId { get; set; } // Reference by ID instead
}
```

### GM005: Invalid property type for INode implementation

**Error**: Properties of `INode` implementations must be simple types, complex types, or collections of simple/complex types (applied recursively).

**Reason**: `INode` implementations can have both simple and complex properties, but all must conform to the graph data model constraints.

**Supported for INode**:
- Simple types (primitives, string, date/time, Point, etc.)
- Complex types (classes with parameterless constructors and only simple properties)
- Collections of simple types
- Collections of complex types

**Example Violation**:
```csharp
// ❌ This will trigger GM005
public class Person : INode
{
    public string Id { get; set; }
    public Dictionary<string, object> Metadata { get; set; } // Unsupported type
    public Stream ProfileImage { get; set; }                // Unsupported type
}
```

**Fix**:
```csharp
// ✅ Use supported types
public class Person : INode
{
    public string Id { get; set; }
    public string MetadataJson { get; set; }    // Simple type
    public Address Address { get; set; }        // Complex type
    public List<string> Tags { get; set; }      // Collection of simple
    public List<PhoneNumber> Phones { get; set; } // Collection of complex
}

public class PhoneNumber
{
    public string Number { get; set; }
    public string Type { get; set; }
}
```

### GM006: Invalid property type for IRelationship implementation

**Error**: Properties of `IRelationship` implementations must be simple types or collections of simple types only.

**Reason**: Relationships should be lightweight and contain only simple data. Complex types are not allowed in relationships to keep them focused and efficient.

**Supported for IRelationship**:
- Simple types only (primitives, string, date/time, Point, etc.)
- Collections of simple types

**Example Violation**:
```csharp
// ❌ This will trigger GM006
public class WorksAt : IRelationship
{
    public string Id { get; set; }
    public DateTime StartDate { get; set; }
    public Address OfficeLocation { get; set; }    // Complex type - not allowed in relationships
    public List<Person> Managers { get; set; }     // Collection of INode - not allowed
}
```

**Fix**:
```csharp
// ✅ Use only simple types
public class WorksAt : IRelationship
{
    public string Id { get; set; }
    public DateTime StartDate { get; set; }
    public string Department { get; set; }         // Simple type
    public List<string> Skills { get; set; }       // Collection of simple types
    public double Salary { get; set; }             // Simple type
}
```

### GM007: Complex properties can only contain simple properties or collections of simple properties

**Error**: Complex properties of `INode` instances can only contain simple properties or collections of simple properties, not other complex properties.

**Reason**: To prevent overly complex nested structures that could impact performance and make the data model difficult to manage. Complex properties should contain only simple, serializable data or collections thereof.

**Example Violation**:
```csharp
// ❌ This will trigger GM007
public class Person : INode
{
    public string Id { get; set; }
    public CompanyInfo Company { get; set; }    // Complex property containing other complex properties
}

public class CompanyInfo
{
    public string Name { get; set; }
    public Address HeadOffice { get; set; }     // Complex property within complex property - not allowed
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}
```

**Fix**:
```csharp
// ✅ Flatten the structure or use only simple properties in complex types
public class Person : INode
{
    public string Id { get; set; }
    public CompanyInfo Company { get; set; }    // Complex property with only simple properties
}

public class CompanyInfo
{
    public string Name { get; set; }
    public string HeadOfficeStreet { get; set; }    // Simple properties only
    public string HeadOfficeCity { get; set; }
    public List<string> Departments { get; set; }   // Collections of simple types allowed
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

1. **Use classes or structs** - Both are now supported for `INode` and `IRelationship` implementations
2. **Provide proper constructors** - Ensure parameterless constructors or property-initializing constructors
3. **Use public properties** with getters and setters/initializers
4. **Avoid node/relationship properties** - Don't use `INode` or `IRelationship` types as properties
5. **Keep relationships simple** - Use only simple types in `IRelationship` implementations
6. **Leverage complex types for nodes** - `INode` can have complex properties if they follow the rules
7. **Consider JSON serialization** for complex data that doesn't need querying

## Requirements

- .NET 10.0 or later
- Graph.Model package

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.
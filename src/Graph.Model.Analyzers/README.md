# Graph Model Analyzers

Roslyn analyzers to enforce the Graph Model's data model rules for types implementing `INode` and `IRelationship` interfaces.

## Supported Rules

### GM001: Missing parameterless constructor or constructor that initializes properties

**Severity**: Error  
**Description**: Types implementing INode or IRelationship must have a parameterless constructor or constructors that initialize their (get/set) properties.

**Examples**:

```csharp
// ✅ Valid - has parameterless constructor
public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

// ✅ Valid - constructor initializes all properties
public class MyNode : INode
{
    public MyNode(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; set; }
    public string Name { get; set; }
}

// ❌ Invalid - no parameterless constructor and doesn't initialize all properties
public class MyNode : INode
{
    public MyNode(string id) => Id = id; // Missing Name initialization

    public string Id { get; set; }
    public string Name { get; set; }
}
```

### GM002: Property must have public getters and setters or initializers

**Severity**: Error  
**Description**: Properties in INode and IRelationship implementations must have public getters and either public setters or public property initializers.

**Examples**:

```csharp
// ✅ Valid - public getter and setter
public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

// ✅ Valid - public getter with init-only setter
public class MyNode : INode
{
    public string Id { get; init; }
    public string Name { get; init; }
}

// ❌ Invalid - private setter
public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; private set; } // Error
}
```

### GM003: Property cannot be INode or IRelationship type

**Severity**: Error  
**Description**: Properties of types implementing INode or IRelationship cannot be INode or IRelationship or collections of them.

**Examples**:

```csharp
// ❌ Invalid - property is INode type
public class MyNode : INode
{
    public string Id { get; set; }
    public INode Parent { get; set; } // Error
}

// ❌ Invalid - property is collection of IRelationship
public class MyNode : INode
{
    public string Id { get; set; }
    public List<IRelationship> Relationships { get; set; } // Error
}
```

### GM004: Invalid property type for INode implementation

**Severity**: Error  
**Description**: Properties of INode implementations must be simple types, complex types, collections of simple types, or collections of complex types, applied recursively.

**Examples**:

```csharp
// ✅ Valid - simple types and collections
public class MyNode : INode
{
    public string Id { get; set; }           // Simple type
    public List<string> Tags { get; set; }   // Collection of simple type
    public Address Location { get; set; }    // Complex type
    public List<Address> Addresses { get; set; } // Collection of complex type
}

public class Address // Complex type
{
    public string Street { get; set; }
    public string City { get; set; }
}
```

### GM005: Invalid property type for IRelationship implementation

**Severity**: Error  
**Description**: Properties of IRelationship implementations must be simple types or collections of simple types.

**Examples**:

```csharp
// ✅ Valid - simple types only
public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
    public List<string> Tags { get; set; }   // Collection of simple type
}

// ❌ Invalid - complex type not allowed in IRelationship
public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
    public Address Location { get; set; }    // Error - complex type
}
```

### GM006: Complex type property contains graph interface types

**Severity**: Error  
**Description**: Properties of complex properties cannot be INode or IRelationship or collections of them. This rule is applied recursively.

**Examples**:

```csharp
// ❌ Invalid - complex type contains INode property
public class Address
{
    public string Street { get; set; }
    public INode OwnerNode { get; set; }     // Error
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Location { get; set; }    // This will trigger GM006
}
```

### GM007: Duplicate PropertyAttribute label in type hierarchy

**Severity**: Error  
**Description**: A type hierarchy cannot have PropertyAttribute annotations with the same Label value across all properties in that type hierarchy.

**Examples**:

```csharp
// ❌ Invalid - duplicate property labels in hierarchy
public class BaseNode : INode
{
    public string Id { get; set; }

    [Property("name")]
    public string Name { get; set; }
}

public class DerivedNode : BaseNode
{
    [Property("name")]                       // Error - duplicate label
    public string DisplayName { get; set; }
}
```

### GM008: Duplicate RelationshipAttribute label in type hierarchy

**Severity**: Error  
**Description**: A type hierarchy cannot have RelationshipAttribute annotations with the same Label value across all types in that type hierarchy.

**Examples**:

```csharp
// ❌ Invalid - duplicate relationship labels in hierarchy
[Relationship("CONNECTS")]
public class BaseRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
}

[Relationship("CONNECTS")]                   // Error - duplicate label
public class SpecificConnection : BaseRelationship
{
    public string ConnectionType { get; set; }
}
```

### GM009: Duplicate NodeAttribute label in type hierarchy

**Severity**: Error  
**Description**: A type hierarchy cannot have NodeAttribute annotations with the same Label value across all types in that type hierarchy.

**Examples**:

```csharp
// ❌ Invalid - duplicate node labels in hierarchy
[Node("Person")]
public class BasePerson : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

[Node("Person")]                             // Error - duplicate label
public class Employee : BasePerson
{
    public string Department { get; set; }
}
```

### GM010: Circular reference without nullable type

**Severity**: Error  
**Description**: A type implementing INode or IRelationship cannot contain a type reference cycle without a nullable type.

**Examples**:

```csharp
// ❌ Invalid - circular reference without nullable
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Person Spouse { get; set; }       // Error - circular reference
}

// ✅ Valid - circular reference with nullable
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Person? Spouse { get; set; }      // OK - nullable breaks the cycle
}
```

## Installation

Add the analyzer package to your project:

```xml
<PackageReference Include="Cvoya.Graph.Model.Analyzers" Version="1.0.0-alpha" PrivateAssets="all" />
```

The analyzers will automatically run during compilation and provide real-time feedback in your IDE.

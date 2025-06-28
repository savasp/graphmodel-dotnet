# Attributes and Configuration

Graph Model uses attributes to provide declarative configuration for how your domain classes map to graph elements. This approach offers clean, type-safe configuration with support for indexing, custom labeling, and property control.

## Node Configuration

### NodeAttribute

The `[Node]` attribute specifies how a class maps to a graph node with custom labeling support.

```csharp
[Node(Label = "Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}
```

**Default Behavior**: Without the attribute, the class name is used as the node label:

```csharp
// This will create nodes with label "Employee"
public class Employee : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
}
```

## Property Configuration

### PropertyAttribute

The `[Property]` attribute provides fine-grained control over property mapping, including custom names, indexing, and serialization behavior:

```csharp
[Node(Label = "Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property(Label = "full_name")]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Property(Label = "birth_date")]
    public DateTime DateOfBirth { get; set; }

    [Property(Ignore = true)]
    public string TemporaryCalculation { get; set; } = string.Empty;

    // This property uses its own name in the graph
    public int Age { get; set; }
}
```

### Property Configuration Options

| Option   | Description                           | Example                           |
| -------- | ------------------------------------- | --------------------------------- |
| `Label`  | Custom property name in graph storage | `[Property(Label = "full_name")]` |
| `Ignore` | Exclude from graph persistence        | `[Property(Ignore = true)]`       |

**Note**: Indexing is handled automatically by the Neo4j provider based on usage patterns and constraints, not through PropertyAttribute. However, attribute-based configuration of indexing behavior is a possible future feature.

### Property Types

Supported property types:

- Primitive types: `int`, `long`, `float`, `double`, `decimal`, `bool`
- `string`
- `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`
- `Guid`
- `Uri`
- Enums
- Arrays and lists of primitive types
- Spatial types (with provider support): `Point`
- "Complex" (as defined by the Graph Model)

```csharp
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public double Height { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string[] Tags { get; set; }
    public List<string> Emails { get; set; }
    public Point Location { get; set; } // Spatial data
}
```

## Relationship Configuration

### RelationshipAttribute

The `[Relationship]` attribute configures how relationship classes map to graph edges, with support for directionality and custom labeling:

```csharp
[Relationship(Label = "KNOWS", Direction = RelationshipDirection.Bidirectional)]
public class Knows : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; } = true;

    [Property(Label = "since_date", Index = true)]
    public DateTime Since { get; set; }
}
```

### Relationship Direction Options

The `RelationshipDirection` enum controls how relationships are interpreted:

```csharp
public enum RelationshipDirection
{
    Outgoing,      // Source -> Target (default)
    Incoming,      // Target -> Source
    Bidirectional  // Source <-> Target
}
```

### Direction Examples

```csharp
// Unidirectional: Person follows another Person
[Relationship(Label = "FOLLOWS", Direction = RelationshipDirection.Outgoing)]
public class Follows : IRelationship
{
    // Implementation...
}

// Bidirectional: Person is friends with another Person
[Relationship(Label = "FRIENDS_WITH", Direction = RelationshipDirection.Bidirectional)]
public class FriendsWith : IRelationship
{
    public bool IsBidirectional { get; set; } = true;
    // Implementation...
}

// Incoming relationship (less common)
[Relationship(Label = "REPORTS_TO", Direction = RelationshipDirection.Incoming)]
public class ReportsTo : IRelationship
{
    // Implementation...
}
```

## Inheritance and Polymorphism

### Base Classes

The Graph Model requires provider implementors to support, if possible, the materialization of object instances to the type that was used during serialization. Consider the following type hierarchy:

```csharp
[Node(Label = "Asset")]
public record Asset : Node
{
    public string Name { get; set; }
    public decimal Value { get; set; }
}

[Node(Label = "Vehicle")]
public record Vehicle : Asset
{
    public string VIN { get; set; }
    public int Year { get; set; }
}

[Node(Label = "RealEstate")]
public record RealEstate : Asset
{
    public string Address { get; set; }
    public double SquareFeet { get; set; }
}
```

We can store a `RealEstate` instance even though the variable that holds it is of type `Asset`.

```csharp
Asset realEstate = new RealEstate { ... }
await graph.CreateNodeAsync(realEstate)
```

The underlying provider serializes the instance of the actual instance, which in this case is `RealEstate`. It stores enough metadata to know the type to be used when retrieving the node from the graph. If a different process, with a completely different type hierarchy is used to retrieve the graph node, the node's label is used in an attempt to identify the right type. In the above case, a type which has been annotated with the attribute `Node(Label = "RealEstate")` will be discovered and the deserialization will be attempted. If the type isn't compatible or a type annotated with that specific label isn't discovered, that is considered a runtime exception.

## Best Practices

1. **Use Meaningful Names**: Choose clear, descriptive names for nodes and relationships
2. **Be Consistent**: Establish naming conventions and stick to them
3. **Document Relationships**: Use XML comments to document complex relationships
4. **Avoid Over-Attribution**: Only add attributes when the default behavior isn't sufficient
5. **Consider Performance**: Indexes and constraints can significantly impact performance

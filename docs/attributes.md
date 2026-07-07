# Attributes and Configuration

> **Note:** To use attribute-based configuration, you only need to install the Neo4j provider package:

> ```bash
> dotnet add package Cvoya.Graph.Model.Neo4j
> ```

> The analyzers package is optional but recommended for extra compile-time validation:

> ```bash
> dotnet add package Cvoya.Graph.Model.Analyzers
> ```

Graph Model uses attributes to provide declarative configuration for how your domain classes map to graph elements. This approach offers clean, type-safe configuration with support for indexing, custom labeling, and property control.

## Node Configuration

### NodeAttribute

The `[Node]` attribute specifies how a class maps to a graph node with custom labeling support.

```csharp
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
}
```

**Default Behavior**: Without the attribute, the class name is used as the node label:

```csharp
// This will create nodes with label "Employee"
public record Employee : Node
{
}
```

## Property Configuration

### PropertyAttribute

The `[Property]` attribute provides fine-grained control over property mapping, including custom names, indexing, and serialization behavior:

```csharp
[Node("Person")]
public record Person : Node
{
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
| `IsIndexed` | Request a provider index for the property | `[Property(IsIndexed = true)]` |

**Note**: Providers decide how to apply requested indexes and constraints for their storage engine.

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
public record Person : Node
{
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

The `[Relationship]` attribute configures how relationship classes map to graph edges by assigning
their stored relationship type:

```csharp
[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    [Property(Label = "since_date", IsIndexed = true)]
    public DateTime Since { get; set; }
}
```

### Relationship Direction Options

`RelationshipDirection` controls storage direction relative to a relationship object's
`StartNodeId`/`EndNodeId` tuple. It does not make a stored edge bidirectional:

```csharp
public enum RelationshipDirection
{
    Outgoing, // Stored as StartNodeId -> EndNodeId (default)
    Incoming  // Stored as EndNodeId -> StartNodeId
}
```

### Direction Examples

```csharp
// Default storage direction: follower -> followed
[Relationship("FOLLOWS")]
public record Follows(string StartNodeId, string EndNodeId)
    : Relationship(StartNodeId, EndNodeId);

// Reversed storage direction relative to the logical tuple
[Relationship("REPORTS_TO")]
public record ReportsTo(string StartNodeId, string EndNodeId)
    : Relationship(StartNodeId, EndNodeId, RelationshipDirection.Incoming);
```

Use `GraphTraversalDirection.Both` on traversal queries when you want to traverse matching stored
edges in either physical direction.

## Inheritance and Polymorphism

### Base Classes

The Graph Model requires provider implementors to support, if possible, the materialization of object instances to the type that was used during serialization. Consider the following type hierarchy:

```csharp
[Node("Asset")]
public record Asset : Node
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

[Node("Vehicle")]
public record Vehicle : Asset
{
    public string VIN { get; set; } = string.Empty;
    public int Year { get; set; }
}

[Node("RealEstate")]
public record RealEstate : Asset
{
    public string Address { get; set; } = string.Empty;
    public double SquareFeet { get; set; }
}
```

We can store a `RealEstate` instance even though the variable that holds it is of type `Asset`.

```csharp
Asset realEstate = new RealEstate
{
    Name = "Office",
    Value = 1_250_000m,
    Address = "123 Main St",
    SquareFeet = 2_400
};
await graph.CreateNodeAsync(realEstate);
```

The underlying provider serializes the instance of the actual instance, which in this case is `RealEstate`. It stores enough metadata to know the type to be used when retrieving the node from the graph. If a different process, with a completely different type hierarchy is used to retrieve the graph node, the node's label is used in an attempt to identify the right type. In the above case, a type which has been annotated with the attribute `Node("RealEstate")` will be discovered and the deserialization will be attempted. If the type isn't compatible or a type annotated with that specific label isn't discovered, that is considered a runtime exception.

## Best Practices

1. **Use Meaningful Names**: Choose clear, descriptive names for nodes and relationships
2. **Be Consistent**: Establish naming conventions and stick to them
3. **Document Relationships**: Use XML comments to document complex relationships
4. **Avoid Over-Attribution**: Only add attributes when the default behavior isn't sufficient
5. **Consider Performance**: Indexes and constraints can significantly impact performance

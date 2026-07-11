---
---

# Attributes and Configuration

> **Note:** To use attribute-based configuration, you only need to install the Neo4j provider package:

> ```bash
> dotnet add package Cvoya.Graph.Neo4j
> ```

> The analyzers package is optional but recommended for extra compile-time validation:

> ```bash
> dotnet add package Cvoya.Graph.Analyzers
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

**One label per type**: a node type maps to exactly one label, and a relationship type to exactly one type name. This label is the correlation key between the stored node and the .NET type used to materialize it (see [Type resolution](#type-resolution) below).

**Uniqueness**: the label must be unique across every node type loaded in the process, compared **case-insensitively** (`Person` and `person` are treated as the same label). Two loaded node types that resolve to the same label — whether both declare it explicitly, or one falls back to a class name that matches another's label — are rejected at registration with a `GraphException`. The same rule applies independently to relationship type names. This mirrors the compile-time analyzers (`CG008`/`CG009`), which flag the collision before you run.

> Multiple labels on a single typed node are not supported. If you need arbitrary, runtime-defined label sets (for example cross-cutting tags), use `DynamicNode`, whose `Labels` collection you manage yourself.

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

## ComplexPropertyAttribute

Complex CLR properties become first-class value nodes connected by a relationship whose type defaults
to the property name. Override that graph name when the domain calls for a different semantic edge:

```csharp
public record Person : Node
{
    [ComplexProperty(RelationshipType = "LIVES_AT")]
    public Address Home { get; init; } = new();
}
```

The resulting structure is `(:Person)-[:LIVES_AT]->(:Address)`. The attribute changes the relationship
mapping only; the CLR property name and serialized value-node label are unchanged.

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

### Type resolution

When materializing a stored node, the provider resolves the .NET type in this order:

1. **Stored metadata (exact).** Each entity is persisted with its concrete .NET type name. If that type is loadable in the reading process and is assignable to the requested type, it is used directly — an exact round-trip.
2. **Label (portable).** If the metadata type is not loadable (a different application, or the type was renamed or moved) or is not assignable to the requested type, the node's **label** is used to find a compatible local type. Because a label maps to exactly one type per process, this is deterministic. The requested type scopes the search, so you materialize the node as the type you asked for (`GetNodeAsync<T>` / `Nodes<T>()`), provided its properties are compatible.
3. **Fallback.** Otherwise the requested type itself is used. For untyped reads, `DynamicNode` always succeeds, exposing the raw labels and properties.

This is why the explicit label is a durable contract: the metadata pointer is a fast path that is allowed to miss, and the label recovers the type across processes and across refactors.

**Polymorphic queries** rely on the class hierarchy rather than on multiple labels. `Nodes<Asset>()` matches not only `:Asset` but also `:Vehicle` and `:RealEstate` — at query-construction time the hierarchy is expanded to the set of compatible labels (each concrete subtype contributes its own single label). Only subtypes the registry has discovered (their assembly is loaded) participate.

## Best Practices

1. **Use Meaningful Names**: Choose clear, descriptive names for nodes and relationships
2. **Be Consistent**: Establish naming conventions and stick to them
3. **Document Relationships**: Use XML comments to document complex relationships
4. **Avoid Over-Attribution**: Only add attributes when the default behavior isn't sufficient
5. **Consider Performance**: Indexes and constraints can significantly impact performance

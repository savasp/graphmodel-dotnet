# Attributes and Configuration

Graph Model uses attributes to provide declarative configuration for how your domain classes map to graph elements. This approach offers clean, type-safe configuration with support for indexing, custom labeling, and property control.

## Node Configuration

### NodeAttribute

The `[Node]` attribute specifies how a class maps to a graph node with custom labeling support.

```csharp
[Node("Person")]
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

### Multiple Labels

Graph providers that support multiple labels can use multiple `[Node]` attributes:

```csharp
[Node("Person")]
[Node("Employee")]
[Node("Manager")]
public class Manager : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}
```

## Property Configuration

### PropertyAttribute

The `[Property]` attribute provides fine-grained control over property mapping, including custom names, indexing, and serialization behavior:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Property(Index = true)]
    public string Email { get; set; } = string.Empty;

    [Property("birth_date", Index = true)]
    public DateTime DateOfBirth { get; set; }

    [Property(Ignore = true)]
    public string TemporaryCalculation { get; set; } = string.Empty;

    // This property uses its own name in the graph
    public int Age { get; set; }
```

### Property Configuration Options

| Option   | Description                           | Example                     |
| -------- | ------------------------------------- | --------------------------- |
| `Label`  | Custom property name in graph storage | `[Property("full_name")]`   |
| `Index`  | Create database index for performance | `[Property(Index = true)]`  |
| `Ignore` | Exclude from graph persistence        | `[Property(Ignore = true)]` |

### Indexing Strategy

Use indexing strategically for properties that are frequently queried:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property(Index = true)] // Frequently searched
    public string Email { get; set; } = string.Empty;

    [Property(Index = true)] // Used in range queries
    public DateTime DateOfBirth { get; set; }

    [Property] // Regular property, no index needed
    public string Bio { get; set; } = string.Empty;
}
}
```

### Property Types

Supported property types:

- Primitive types: `int`, `long`, `float`, `double`, `decimal`, `bool`
- `string`
- `DateTime`, `DateTimeOffset`
- `Guid`
- Arrays and lists of primitive types
- Spatial types (with provider support): `Point`

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
[Relationship("KNOWS", Direction = RelationshipDirection.Bidirectional)]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; } = true;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property("since_date", Index = true)]
    public DateTime Since { get; set; }

    [Property]
    public string Relationship { get; set; } = string.Empty;
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
[Relationship("FOLLOWS", Direction = RelationshipDirection.Outgoing)]
public class Follows : IRelationship<Person, Person>
{
    // Implementation...
}

// Bidirectional: Person is friends with another Person
[Relationship("FRIENDS_WITH", Direction = RelationshipDirection.Bidirectional)]
public class FriendsWith : IRelationship<Person, Person>
{
    public bool IsBidirectional { get; set; } = true;
    // Implementation...
}

// Incoming relationship (less common)
[Relationship("REPORTS_TO", Direction = RelationshipDirection.Incoming)]
public class ReportsTo : IRelationship<Person, Person>
{
    // Implementation...
}
```

```csharp
[Relationship("FRIEND_OF")]
public class FriendOf : IRelationship<Person, Person>
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; } = true; // Bidirectional

    public Person Source { get; set; }
    public Person Target { get; set; }
}
```

### Relationship Direction

Control how relationships are interpreted:

```csharp
[Relationship("MANAGES", Direction = RelationshipDirection.Outgoing)]
public class Manages : IRelationship<Manager, Employee>
{
    // Manager -> Employee
}

[Relationship("REPORTS_TO", Direction = RelationshipDirection.Incoming)]
public class ReportsTo : IRelationship<Employee, Manager>
{
    // Employee -> Manager (but stored as Manager <- Employee)
}
```

## Navigation Properties

### Collection Navigation

Define collections for relationship navigation:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public string Name { get; set; }

    // Navigation properties
    public IList<Knows> KnowsRelationships { get; set; } = new List<Knows>();
    public IList<WorksAt> Employment { get; set; } = new List<WorksAt>();
}
```

### Lazy Loading Configuration

Control when navigation properties are loaded:

```csharp
[Node("Department")]
public class Department : INode
{
    public string Id { get; set; }
    public string Name { get; set; }

    [LazyLoad] // Only loaded when accessed
    public IList<Employee> Employees { get; set; }
}
```

## Advanced Configuration

### Composite Keys

For scenarios requiring composite unique constraints:

```csharp
[Node("Product")]
[CompositeKey(nameof(SKU), nameof(WarehouseId))]
public class InventoryItem : INode
{
    public string Id { get; set; }
    public string SKU { get; set; }
    public string WarehouseId { get; set; }
    public int Quantity { get; set; }
}
```

### Indexes

Specify properties that should be indexed:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }

    [Index]
    public string Email { get; set; }

    [Index]
    public string PhoneNumber { get; set; }

    [Index(IsUnique = true)]
    public string SocialSecurityNumber { get; set; }
}
```

### Constraints

Add constraints to your model:

```csharp
[Node("User")]
public class User : INode
{
    public string Id { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Username { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Range(0, 150)]
    public int Age { get; set; }
}
```

## Custom Type Mappings

### Type Converters

For custom type conversions:

```csharp
[Node("Event")]
public class Event : INode
{
    public string Id { get; set; }
    public string Name { get; set; }

    [TypeConverter(typeof(JsonTypeConverter))]
    public EventMetadata Metadata { get; set; }
}

public class JsonTypeConverter : ITypeConverter<EventMetadata>
{
    public string Convert(EventMetadata value)
    {
        return JsonSerializer.Serialize(value);
    }

    public EventMetadata ConvertBack(string value)
    {
        return JsonSerializer.Deserialize<EventMetadata>(value);
    }
}
```

### Spatial Data

Working with geographic data:

```csharp
[Node("Location")]
public class Location : INode
{
    public string Id { get; set; }
    public string Name { get; set; }

    [Property("coordinates")]
    public Point Coordinates { get; set; }

    // Calculate distance using provider-specific functions
    public double DistanceTo(Point other)
    {
        // Implementation depends on provider
    }
}

// Usage
var location = new Location
{
    Name = "Seattle",
    Coordinates = new Point { X = -122.3321, Y = 47.6062 }
};
```

## Inheritance and Polymorphism

### Base Classes

```csharp
[Node("Asset")]
public abstract class Asset : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public decimal Value { get; set; }
}

[Node("Vehicle")]
public class Vehicle : Asset
{
    public string VIN { get; set; }
    public int Year { get; set; }
}

[Node("RealEstate")]
public class RealEstate : Asset
{
    public string Address { get; set; }
    public double SquareFeet { get; set; }
}
```

### Discriminators

For single-table inheritance:

```csharp
[Node("Animal")]
[Discriminator("AnimalType")]
public abstract class Animal : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}

[DiscriminatorValue("Dog")]
public class Dog : Animal
{
    public string Breed { get; set; }
}

[DiscriminatorValue("Cat")]
public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}
```

## Validation Attributes

Combine with validation frameworks:

```csharp
[Node("Product")]
public class Product : INode, IValidatableObject
{
    public string Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Url]
    public string ProductUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Price < Cost)
        {
            yield return new ValidationResult(
                "Price must be greater than cost",
                new[] { nameof(Price) }
            );
        }
    }
}
```

## Best Practices

1. **Use Meaningful Names**: Choose clear, descriptive names for nodes and relationships
2. **Be Consistent**: Establish naming conventions and stick to them
3. **Document Relationships**: Use XML comments to document complex relationships
4. **Avoid Over-Attribution**: Only add attributes when the default behavior isn't sufficient
5. **Consider Performance**: Indexes and constraints can significantly impact performance

```csharp
/// <summary>
/// Represents a person in the social network
/// </summary>
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }

    /// <summary>
    /// The person's display name
    /// </summary>
    [Property("display_name")]
    [Required]
    public string DisplayName { get; set; }

    /// <summary>
    /// Relationships to other people this person knows
    /// </summary>
    public IList<Knows> Connections { get; set; } = new List<Knows>();
}
```

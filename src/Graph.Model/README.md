# Graph.Model

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

A powerful and flexible .NET library for working with graph data structures. Graph.Model provides a clean abstraction layer over graph databases with advanced querying, transaction management, and relationship traversal capabilities.

## 🌟 Features

- **🔒 Type-safe graph operations** - Work with strongly-typed nodes and relationships
- **🔍 Enhanced LINQ support** - Query your graph using familiar LINQ syntax with graph-specific extensions
- **🔄 Advanced querying** - Graph traversals, pattern matching, and complex multi-step queries
- **⚡ Transaction support** - Full ACID transaction management with async/await support
- **🎯 Flexible traversal** - Control relationship loading depth and behavior with fine-grained options
- **📈 Performance optimizations** - Query caching, profiling, and optimization hints
- **🏗️ Provider agnostic** - Clean abstraction that can be implemented by different graph databases
- **🎨 Attribute-based configuration** - Configure nodes and relationships using attributes with indexing and custom labeling
- **⚙️ Modern C# features** - Built for .NET 8+ with nullable reference types and latest C# features

## 🚀 Quick Start

### Installation

```bash
dotnet add package Cvoya.Graph.Model
```

### Basic Usage

```csharp
using Cvoya.Graph.Model;

// Define your nodes
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("first_name", Index = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name", Index = true)]
    public string LastName { get; set; } = string.Empty;

    [Property]
    public int Age { get; set; }

    [Property]
    public Address? HomeAddress { get; set; } // Complex types supported
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

// Define your relationships
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property]
    public DateTime Since { get; set; }

    [Property]
    public string RelationshipType { get; set; } = "friend";
}

// Use the graph (example with a provider)
await using var graph = GetGraphInstance(); // Implementation-specific

// Create nodes
var alice = new Person
{
    FirstName = "Alice",
    LastName = "Smith",
    Age = 30,
    HomeAddress = new Address
    {
        Street = "123 Main St",
        City = "Portland",
        Country = "USA"
    }
};

var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };

await graph.CreateNodeAsync(alice);
await graph.CreateNodeAsync(bob);

// Create relationships
var friendship = new Knows
{
    StartNodeId = alice.Id,
    EndNodeId = bob.Id,
    Since = DateTime.UtcNow.AddYears(-2),
    RelationshipType = "close friend"
};
await graph.CreateRelationshipAsync(friendship);

// Query with enhanced LINQ
var youngPeople = await graph.Nodes<Person>()
    .Where(p => p.Age < 30)
    .Where(p => p.HomeAddress != null && p.HomeAddress.City == "Portland")
    .OrderBy(p => p.FirstName)
    .ToListAsync();

// Advanced traversal queries
var alicesFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 2)
    .Where(friend => friend.Age > 20)
    .ToListAsync();

// Transaction management
await using var transaction = await graph.GetTransactionAsync();
try
{
    await graph.CreateNodeAsync(newPerson, transaction: transaction);
    await graph.CreateRelationshipAsync(newRelationship, transaction: transaction);
    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

## 🏗️ Core Concepts

### Entities: Nodes and Relationships

All graph entities implement the `IEntity` interface, providing a common `Id` property:

```csharp
public interface IEntity
{
    string Id { get; init; }
}
```

### Nodes

Nodes represent entities in your graph. Any class implementing `INode` can be stored as a node:

```csharp
public interface INode : IEntity
{
    // Marker interface - extends IEntity
}
```

Use the `[Node]` attribute to specify custom labels and the `[Property]` attribute to control property mapping:

```csharp
[Node("Employee", "Person")] // Multiple labels supported
public class Employee : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("emp_id", Index = true)]
    public string EmployeeId { get; set; } = string.Empty;

    [Property(Index = true)]
    public string Department { get; set; } = string.Empty;

    [Property(Ignore = true)] // Excluded from persistence
    public string FullDisplayName => $"{FirstName} {LastName} ({EmployeeId})";
}
```

### Relationships

Relationships connect nodes and can carry properties. There are two main interfaces:

#### Basic Relationships

```csharp
public interface IRelationship : IEntity
{
    RelationshipDirection Direction { get; init; }
    string StartNodeId { get; init; }
    string EndNodeId { get; init; }
}
```

#### Strongly-Typed Relationships

```csharp
public interface IRelationship<TSource, TTarget> : IRelationship
    where TSource : class, INode, new()
    where TTarget : class, INode, new()
{
    TSource Source { get; set; }
    TTarget Target { get; set; }
}
```

Example with navigation properties:

```csharp
[Relationship("MANAGES")]
public class Manages : IRelationship<Manager, Employee>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public Manager? Source { get; set; }  // Navigation property
    public Employee? Target { get; set; } // Navigation property

    [Property]
    public DateTime StartDate { get; set; }

    [Property]
    public string ManagementLevel { get; set; } = "Direct";
}
```

### Transactions

Full ACID transaction support ensures data consistency:

```csharp
public interface IGraphTransaction : IAsyncDisposable
{
    Task Commit(CancellationToken cancellationToken = default);
    Task Rollback(CancellationToken cancellationToken = default);
}
```

### Enhanced Querying

The library provides `IGraphQueryable<T>` which extends standard LINQ with graph-specific operations:

```csharp
// Standard LINQ operations
var results = graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .OrderBy(p => p.LastName)
    .Take(10)
    .ToList();

// Graph-specific operations
var traversalResults = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 3)                    // Depth control
    .Direction(TraversalDirection.Both) // Direction control
    .ToList();

// Path segments for complex analysis
var pathSegments = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .Where(ps => ps.EndNode.Age > 30)
    .Select(ps => new {
        Start = ps.StartNode.FirstName,
        Relationship = ps.Relationship.Since,
        End = ps.EndNode.FirstName
    })
    .ToList();
```

## 🎨 Configuration with Attributes

### Node Attribute

```csharp
[Node("Person")]                    // Single label
[Node("Employee", "Person")]        // Multiple labels
public class Person : INode { }
```

### Relationship Attribute

```csharp
[Relationship("KNOWS")]
[Relationship("FOLLOWS", Direction = RelationshipDirection.Outgoing)]
public class Follows : IRelationship { }
```

### Property Attribute

```csharp
public class Person : INode
{
    [Property(Label = "first_name")]        // Custom name
    public string FirstName { get; set; } = string.Empty;

    [Property]                              // Auto name (uses property name)
    public string Email { get; set; } = string.Empty;

    [Property(Ignore = true)]               // Excluded from persistence
    public string DisplayName => $"{FirstName} {LastName}";
}
```

## 🔄 Graph Traversal

### Basic Traversal

```csharp
// Get all friends of Alice
var friends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .ToListAsync();
```

### Depth-Controlled Traversal

```csharp
// Friends and friends-of-friends (1-2 hops)
var extendedNetwork = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 2)
    .ToListAsync();
```

### Directional Traversal

```csharp
// Only outgoing relationships
var following = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Follows, Person>()
    .Direction(TraversalDirection.Outgoing)
    .ToListAsync();
```

### Path Analysis

```csharp
// Analyze the full path information
var socialPaths = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .Where(ps => ps.EndNode.City == "Portland")
    .Select(ps => new {
        PersonA = ps.StartNode.FirstName,
        PersonB = ps.EndNode.FirstName,
        FriendsSince = ps.Relationship.Since,
        PathLength = 1 // This would be calculated in real scenarios
    })
    .ToListAsync();
```

## 📊 Complex Object Support

Graph.Model automatically handles complex properties through serialization:

```csharp
public class Company : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property]
    public string Name { get; set; } = string.Empty;

    [Property]
    public Address Headquarters { get; set; } = new();

    [Property]
    public List<Address> Offices { get; set; } = new();

    [Property]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public Point? Coordinates { get; set; } // Spatial data
}
```

## 🎯 Provider Architecture

Graph.Model is designed to work with multiple graph database providers. To implement a provider:

1. Implement `IGraph` interface
2. Implement `IGraphQueryProvider` for LINQ support
3. Implement `IGraphTransaction` for transaction management
4. Handle entity serialization/deserialization

## 📚 Documentation

- **[Getting Started](docs/getting-started.md)** - Complete walkthrough with examples
- **[Core Interfaces](docs/core-interfaces.md)** - Understanding the type system
- **[Querying with LINQ](docs/querying.md)** - Advanced query patterns
- **[Transactions](docs/transactions.md)** - Transaction management patterns
- **[Attributes and Configuration](docs/attributes.md)** - Customizing behavior
- **[Best Practices](docs/best-practices.md)** - Performance and design guidance

## 🔧 Requirements

- **.NET 8.0** or later
- **C# 12** language features
- A compatible graph provider (e.g., `Cvoya.Graph.Model.Neo4j`)

## 📄 License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.

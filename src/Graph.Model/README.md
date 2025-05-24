# Graph Model

A powerful and flexible .NET library for working with graph data structures. Graph Model provides a clean abstraction layer over graph databases with support for LINQ queries, transactions, and relationship traversal.

## Features

- **Type-safe graph operations** - Work with strongly-typed nodes and relationships
- **LINQ support** - Query your graph using familiar LINQ syntax
- **Transaction support** - Ensure data consistency with transaction management
- **Flexible traversal** - Control relationship loading depth and behavior
- **Provider agnostic** - Clean abstraction that can be implemented by different graph databases
- **Attribute-based configuration** - Configure nodes and relationships using attributes

## Quick Start

```csharp
// Define your nodes
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

// Define your relationships
[Relationship("KNOWS")]
public class Knows : Relationship<Person, Person>
{
    public DateTime Since { get; set; }
}

// Use the graph
using var graph = GetGraphInstance(); // Implementation-specific

// Create nodes
var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 30 };
var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNode(alice);
await graph.CreateNode(bob);

// Create relationships
var knows = new Knows { Source = alice, Target = bob, Since = DateTime.UtcNow };
await graph.CreateRelationship(knows);

// Query with LINQ
var friends = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .SelectMany(p => p.Knows.Select(k => k.Target))
    .ToList();
```

## Core Concepts

### Nodes

Nodes represent entities in your graph. Any class implementing `INode` can be stored as a node.

### Relationships

Relationships connect nodes and can carry properties. Implement `IRelationship` for basic relationships or use `IRelationship<TSource, TTarget>` for typed relationships with navigation.

### Transactions

Support for ACID transactions ensures data consistency during complex operations.

### Traversal Options

Control how deep the graph should load relationships when querying nodes.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Core Interfaces](docs/core-interfaces.md)
- [Querying with LINQ](docs/querying.md)
- [Transactions](docs/transactions.md)
- [Attributes and Configuration](docs/attributes.md)
- [Best Practices](docs/best-practices.md)

## Installation

```bash
dotnet add package Cvoya.Graph.Model
```

## Requirements

- .NET 10.0 or later

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.

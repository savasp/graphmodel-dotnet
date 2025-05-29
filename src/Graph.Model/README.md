# Graph Model

A powerful and flexible .NET library for working with graph data structures. Graph Model provides a clean abstraction layer over graph databases with advanced querying, transaction management, and relationship traversal capabilities.

## Features

- **Type-safe graph operations** - Work with strongly-typed nodes and relationships
- **Enhanced LINQ support** - Query your graph using familiar LINQ syntax with graph-specific extensions
- **Advanced querying** - Graph traversals, pattern matching, and complex multi-step queries
- **Transaction support** - Full ACID transaction management with async/await support
- **Flexible traversal** - Control relationship loading depth and behavior with fine-grained options
- **Performance optimizations** - Query caching, profiling, and optimization hints
- **Provider agnostic** - Clean abstraction that can be implemented by different graph databases
- **Attribute-based configuration** - Configure nodes and relationships using attributes with indexing and custom labeling
- **Modern C# features** - Built for .NET 10 with nullable reference types and latest C# features

## Quick Start

```csharp
// Define your nodes
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Property(Index = true)]
    public int Age { get; set; }
}

// Define your relationships
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; }

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    public DateTime Since { get; set; }
}

// Use the graph
await using var graph = GetGraphInstance(); // Implementation-specific

// Create nodes
var alice = new Person { FirstName = "Alice", LastName = "Smith", Age = 30 };
var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNode(alice);
await graph.CreateNode(bob);

// Create relationships
var knows = new Knows
{
    SourceId = alice.Id,
    TargetId = bob.Id,
    Since = DateTime.UtcNow
};
await graph.CreateRelationship(knows);

// Query with enhanced LINQ
var friends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .WithDepth(2) // Load relationships up to 2 levels deep
    .FirstOrDefaultAsync();

// Advanced traversal queries
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows>()
    .InDirection(TraversalDirection.Outgoing)
    .WithDepth(1, 2)
    .ToTarget<Person>()
    .ToListAsync();

// Transaction management
await using var transaction = await graph.BeginTransaction();
try
{
    await graph.CreateNode(alice, transaction: transaction);
    await graph.CreateNode(bob, transaction: transaction);
    await graph.CreateRelationship(knows, transaction: transaction);
    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

## Core Concepts

### Nodes

Nodes represent entities in your graph. Any class implementing `INode` can be stored as a node. Use the `[Node]` attribute to specify custom labels, and `[Property]` attributes to control property mapping and indexing.

### Relationships

Relationships connect nodes and can carry properties. Implement `IRelationship` for basic relationships or use `IRelationship<TSource, TTarget>` for strongly-typed relationships with navigation properties. Use the `[Relationship]` attribute to specify custom labels and direction.

### Transactions

Full ACID transaction support with async/await patterns ensures data consistency during complex operations. Transactions implement `IAsyncDisposable` for proper resource management.

### Enhanced Querying

The library provides `IGraphQueryable<T>` which extends standard LINQ with graph-specific operations like:

- **Traversal control** - Specify depth limits and traversal directions
- **Graph patterns** - Match complex graph structures
- **Performance optimization** - Query caching, hints, and profiling
- **Metadata inclusion** - Access query execution details

### Operation Options

`GraphOperationOptions` provides fine-grained control over graph operations including cascade delete behavior and other provider-specific settings.

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

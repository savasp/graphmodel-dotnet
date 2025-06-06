# Graph.Provider.Neo4j

High-performance Neo4j implementation of the Graph.Model abstraction layer, providing seamless LINQ-to-Cypher translation, transaction management, and advanced graph operations.

## Overview

This package provides a complete implementation of the `IGraph` interface for Neo4j graph databases, enabling seamless integration with the Graph.Model abstraction layer. It translates LINQ queries to optimized Cypher queries, manages Neo4j transactions, and handles complex property serialization automatically.

## Features

- **LINQ-to-Cypher Translation** - Automatic conversion of LINQ queries to optimized Cypher
- **Full `IGraphQueryable<T>` Support** - Advanced graph querying with depth control and traversal
- **Transaction Management** - Complete ACID transaction support with proper resource disposal
- **Complex Property Serialization** - Automatic handling of complex types using `__PROPERTY__` relationships
- **Constraint Management** - Automatic creation and management of Neo4j constraints and indexes
- **Connection Pooling** - Efficient connection management using Neo4j driver pooling
- **Query Optimization** - Performance hints, caching, and query profiling support
- **Spatial Data Support** - Native handling of `Point` types for spatial queries
- **Bulk Operations** - Optimized batch processing for large datasets

## Installation

```bash
dotnet add package Cvoya.Graph.Model.Neo4j
```

## Requirements

- .NET 10.0 or later
- Neo4j 4.0 or later (5.x recommended)
- Graph.Model package

## Quick Start

### Basic Setup

```csharp
using Cvoya.Graph.Model.Neo4j;
using Neo4j.Driver;

// Option 1: Using connection string (simplest)
var graph = new Neo4jGraphProvider(
    uri: "bolt://localhost:7687",
    username: "neo4j",
    password: "password",
    databaseName: "neo4j"
);

// Option 2: Using existing Neo4j driver
var driver = GraphDatabase.Driver("bolt://localhost:7687",
    AuthTokens.Basic("neo4j", "password"));
var graph = new Neo4jGraphProvider(driver, "neo4j");

// Option 3: Using environment variables
// NEO4J_URI, NEO4J_USER, NEO4J_PASSWORD, NEO4J_DATABASE
var graph = new Neo4jGraphProvider(); // Reads from environment
```

### Configuration with Environment Variables

Set these environment variables for automatic configuration:

```bash
export NEO4J_URI="bolt://localhost:7687"
export NEO4J_USER="neo4j"
export NEO4J_PASSWORD="your-password"
export NEO4J_DATABASE="neo4j"
```

Then simply:

```csharp
var graph = new Neo4jGraphProvider(); // Automatically configured
```

## Usage Examples

### Domain Model Definition

```csharp
using Cvoya.Graph.Model;

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
    public Address? HomeAddress { get; set; } // Complex type - auto-serialized
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public Point? Coordinates { get; set; } // Spatial data
}

[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; } = true;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property]
    public DateTime Since { get; set; }

    [Property]
    public string RelationshipType { get; set; } = "friend";
}
```

### Basic CRUD Operations

```csharp
await using var graph = new Neo4jGraphProvider();

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
        Country = "USA",
        Coordinates = new Point(45.5152, -122.6784) // Portland coordinates
    }
};

await graph.CreateNode(alice);

var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNode(bob);

// Create relationships
var friendship = new Knows
{
    SourceId = alice.Id,
    TargetId = bob.Id,
    Since = DateTime.UtcNow.AddYears(-2),
    RelationshipType = "close friend"
};

await graph.CreateRelationship(friendship);
```

### Advanced LINQ Queries

```csharp
// Complex LINQ queries are automatically translated to optimized Cypher
var youngFriends = await graph.Nodes<Person>()
    .Where(p => p.Age < 30)
    .Where(p => p.HomeAddress != null && p.HomeAddress.City == "Portland")
    .OrderBy(p => p.LastName)
    .ThenBy(p => p.FirstName)
    .ToListAsync();

// Graph-specific operations
var friendsOfAlice = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .WithDepth(2) // Load relationships up to 2 levels deep
    .FirstOrDefaultAsync();

// Traversal queries
var friendsOfFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Person, Knows>()
    .InDirection(TraversalDirection.Outgoing)
    .WithDepth(1, 2)
    .ToTarget<Person>()
    .Where(friend => friend.Age > 20)
    .ToListAsync();

// Spatial queries
var nearbyPeople = await graph.Nodes<Person>()
    .Where(p => p.HomeAddress != null &&
                p.HomeAddress.Coordinates != null &&
                p.HomeAddress.Coordinates.Distance(myLocation) < 1000) // Within 1km
    .ToListAsync();
```

### Transaction Management

```csharp
// Automatic transaction management
await using var transaction = await graph.BeginTransaction();
try
{
    await graph.CreateNode(alice, transaction: transaction);
    await graph.CreateNode(bob, transaction: transaction);
    await graph.CreateRelationship(friendship, transaction: transaction);

    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}

// Using statement automatically handles rollback on exception
await using var tx = await graph.BeginTransaction();
await graph.CreateNode(alice, transaction: tx);
await graph.CreateNode(bob, transaction: tx);
await tx.Commit(); // Explicit commit
```

### Query Optimization

```csharp
// Using query hints for optimization
var optimizedQuery = graph.Nodes<Person>()
    .WithHint("USE INDEX")
    .WithIndex("person_name_index")
    .Where(p => p.LastName == "Smith")
    .UseCache(TimeSpan.FromMinutes(5)); // Cache results

// Query profiling
var profiledQuery = graph.Nodes<Person>()
    .WithProfiling(true)
    .Where(p => p.Age > 25);

var results = await profiledQuery.ToListAsync();
var metadata = profiledQuery.GetMetadata(); // Access query statistics
```

## Neo4j-Specific Features

### Constraint and Index Management

The provider automatically creates and manages Neo4j constraints and indexes based on your domain model attributes:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("email", Index = true)]
    public string Email { get; set; } = string.Empty; // Creates index

    [Property("name", Index = true)]
    public string Name { get; set; } = string.Empty; // Creates index
}
```

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("email", Index = true)]
    public string Email { get; set; } = string.Empty; // Creates index

    [Property("name", Index = true)]
    public string Name { get; set; } = string.Empty; // Creates index
}
```

### Complex Property Serialization

Complex properties are automatically serialized as `__PROPERTY__` relationships:

```csharp
// This address will be stored as a separate __PROPERTY__ relationship
public class Person : INode
{
    public Address HomeAddress { get; set; }
    public List<Address> PreviousAddresses { get; set; } // Collections also supported
}
```

### Spatial Data Support

```csharp
// Point types are natively supported by Neo4j
public class Location : INode
{
    public Point Coordinates { get; set; }
}

// Spatial queries
var nearbyLocations = await graph.Nodes<Location>()
    .Where(l => l.Coordinates.Distance(centerPoint) < 1000)
    .ToListAsync();
```

## Configuration Options

### Connection Configuration

```csharp
var config = Config.Builder()
    .WithMaxConnectionPoolSize(100)
    .WithConnectionTimeout(TimeSpan.FromSeconds(30))
    .WithEncryption(false) // For local development
    .Build();

var driver = GraphDatabase.Driver("bolt://localhost:7687",
    AuthTokens.Basic("neo4j", "password"), config);

var graph = new Neo4jGraphProvider(driver, "neo4j");
```

### Performance Tuning

```csharp
// Configure query execution
var graph = new Neo4jGraphProvider(driver, "neo4j", logger)
{
    DefaultQueryTimeout = TimeSpan.FromSeconds(30),
    EnableQueryCaching = true,
    MaxCacheSize = 1000
};
```

## Best Practices

1. **Use connection pooling** - Reuse `Neo4jGraphProvider` instances instead of creating new ones
2. **Leverage indexes** - Use `[Property(Index = true)]` for frequently queried properties
3. **Optimize queries** - Use `WithDepth()` to control relationship loading
4. **Handle transactions properly** - Always use `using` statements or explicit resource disposal
5. **Monitor performance** - Use query profiling to identify slow queries
6. **Use spatial indexes** - For spatial data, consider creating spatial indexes in Neo4j

## Error Handling

The provider translates Neo4j-specific errors to Graph.Model exceptions:

```csharp
try
{
    await graph.CreateNode(person);
}
catch (GraphException ex)
{
    // Handle graph-specific errors
    logger.LogError(ex, "Failed to create node");
}
catch (GraphTransactionException ex)
{
    // Handle transaction-specific errors
    logger.LogError(ex, "Transaction failed");
}
```

## Logging

The provider supports structured logging through `Microsoft.Extensions.Logging`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<Neo4jGraphProvider>();

var graph = new Neo4jGraphProvider(driver, "neo4j", logger);
```

## Examples

For complete working examples, see the [examples folder](../../examples) in the repository:

- [Basic CRUD Operations](../../examples/Example1.BasicCRUD)
- [LINQ and Traversal](../../examples/Example2.LINQAndTraversal)
- [Transaction Management](../../examples/Example3.TransactionManagement)
- [Advanced Scenarios](../../examples/Example4.AdvancedScenarios)
- [Social Network](../../examples/Example5.SocialNetwork)

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.

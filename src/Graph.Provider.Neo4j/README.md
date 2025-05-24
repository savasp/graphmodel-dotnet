# Graph.Provider.Neo4j

A Neo4j implementation of the Graph Model library, providing a high-performance graph database backend with full support for LINQ queries, transactions, and relationship traversal.

## Features

- **Full Graph Model compatibility** - Implements all Graph Model interfaces
- **LINQ to Cypher translation** - Write queries in C# that execute as optimized Cypher
- **Transaction support** - Full ACID transaction support via Neo4j
- **Automatic constraint management** - Creates indexes and constraints as needed
- **Relationship traversal** - Configurable depth-based loading of related entities
- **Connection pooling** - Built on the official Neo4j .NET driver

## Installation

```bash
dotnet add package Cvoya.Graph.Provider.Neo4j
```

## Quick Start

```csharp
using Cvoya.Graph.Provider.Neo4j;
using Cvoya.Graph.Model;

// Create a provider instance
var graph = new Neo4jGraphProvider(
    uri: "bolt://localhost:7687",
    username: "neo4j",
    password: "password",
    databaseName: "neo4j"
);

// Or use environment variables (NEO4J_URI, NEO4J_USER, NEO4J_PASSWORD, NEO4J_DATABASE)
var graph = new Neo4jGraphProvider();

// Use the graph - see Graph.Model documentation for full API
var person = new Person { FirstName = "Alice", LastName = "Smith" };
await graph.CreateNode(person);

// Query with LINQ
var results = graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .ToList();
```

## Configuration

### Connection Options

The provider can be configured through the constructor or environment variables:

```csharp
// Constructor parameters
var graph = new Neo4jGraphProvider(
    uri: "bolt://localhost:7687",      // Neo4j connection URI
    username: "neo4j",                 // Username
    password: "password",              // Password
    databaseName: "mydb",              // Database name (default: "neo4j")
    logger: myLogger                   // Optional ILogger instance
);
```

### Environment Variables

- `NEO4J_URI` - Connection URI (default: bolt://localhost:7687)
- `NEO4J_USER` - Username (default: neo4j)
- `NEO4J_PASSWORD` - Password (default: password)
- `NEO4J_DATABASE` - Database name (default: neo4j)

## Neo4j-Specific Features

### Direct Cypher Queries

Execute Cypher queries directly when needed:

```csharp
var results = await graph.ExecuteCypher(
    "MATCH (p:Person)-[:KNOWS]->(friend) WHERE p.FirstName = $name RETURN friend",
    new { name = "Alice" }
);
```

### Automatic Constraint Creation

The provider automatically creates constraints for:

- Unique `Id` property on all node types
- Required properties marked with attributes

## Performance Considerations

- **Connection pooling** is handled automatically by the Neo4j driver
- **Batch operations** within transactions for better performance
- **Traversal depth** can be controlled to optimize queries
- **LINQ queries** are translated to efficient Cypher

## Logging

The provider supports Microsoft.Extensions.Logging:

```csharp
var logger = LoggerFactory
    .Create(builder => builder.AddConsole())
    .CreateLogger<Neo4jGraphProvider>();

var graph = new Neo4jGraphProvider(logger: logger);
```

## Documentation

For detailed documentation on graph operations, LINQ queries, and transactions, see the [Graph.Model documentation](../Graph.Model/README.md).

## Requirements

- .NET 10.0 or later
- Neo4j 4.4 or later
- Graph.Model package

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../../LICENSE) for details.

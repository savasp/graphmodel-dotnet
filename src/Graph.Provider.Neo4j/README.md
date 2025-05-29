# Graph.Provider.Neo4j

Neo4j implementation of the Graph.Model abstraction layer.

## Overview

This package provides a complete implementation of the `IGraph` interface for Neo4j graph databases, enabling seamless integration with the Graph.Model abstraction layer.

## Features

- Full LINQ support for graph queries
- Complex property serialization with `__PROPERTY__` relationships
- Graph traversal operations
- Transaction management
- Neo4j-specific optimizations

## Usage

```csharp
using Cvoya.Graph.Provider.Neo4j;

// Create a Neo4j graph provider
var provider = new Neo4jGraphProvider(connectionString);

// Use with Graph.Model interfaces
IGraph graph = provider;
```

For more detailed examples, see the examples folder in the root of this repository.

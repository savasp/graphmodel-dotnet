# GraphModel

A comprehensive .NET library ecosystem for working with graph data structures and graph databases, providing type-safe abstractions, LINQ support, and seamless integration with graph database providers.

Additional context for the project from [Savas Parastatidis](https://savas.me)' blog: [Playing with graphs and neo4j](https://savas.me/2025/05/23/playing-with-graphs-and-neo4j/).

GraphModel consists of three main packages:

- **Graph.Model**: A powerful and flexible abstraction for graph data, supporting type-safe nodes and relationships, LINQ queries, transactions, and provider-agnostic architecture.
- **Graph.Provider.Neo4j**: A high-performance Neo4j backend for Graph.Model, translating LINQ queries to Cypher, managing transactions, and providing seamless integration with Neo4j databases.
- **Graph.Model.Analyzers**: Compile-time code analyzers that ensure proper implementation of `INode` and `IRelationship` interfaces, helping catch common errors during development.

## Overview

GraphModel enables developers to model, query, and manipulate graph data in .NET applications with a clean, type-safe API. The core library (`Graph.Model`) defines the abstractions and interfaces for graph operations, while provider packages (such as `Graph.Provider.Neo4j`) implement these abstractions for specific graph databases.

### Key Features

- **Strongly-typed graph operations** - Work with type-safe nodes and relationships using modern C# features
- **Enhanced LINQ support** - Query your graph using familiar LINQ syntax with graph-specific extensions
- **ACID transaction support** - Full transaction management with async/await patterns
- **Advanced querying** - Graph traversals, pattern matching, and complex multi-step queries
- **Provider-agnostic design** - Clean abstraction that works with different graph database backends
- **Neo4j provider** - Complete implementation with LINQ-to-Cypher translation, constraint management, and connection pooling
- **Compile-time validation** - Code analyzers ensure proper implementation of graph interfaces
- **Flexible relationship traversal** - Control loading depth and behavior with fine-grained options
- **Attribute-based configuration** - Configure nodes and relationships using attributes with indexing and custom labeling
- **Performance optimizations** - Query caching, profiling, and optimization hints

## Getting Started

1. **Install the core library:**

   ```bash
   dotnet add package Cvoya.Graph.Model
   ```

2. **(Optional) Install a graph provider (e.g., Neo4j):**

   ```bash
   dotnet add package Cvoya.Graph.Model.Neo4j
   ```

3. **(Recommended) Install compile-time analyzers:**

   ```bash
   dotnet add package Cvoya.Graph.Model.Analyzers
   ```

4. **Define your graph model and use the API as described in the [Graph.Model documentation](src/Graph.Model/README.md).**

For more details, see the individual package READMEs:

- [Graph.Model](src/Graph.Model/README.md) - Core abstractions and interfaces
- [Graph.Provider.Neo4j](src/Graph.Provider.Neo4j/README.md) - Neo4j implementation
- [Graph.Model.Analyzers](src/Graph.Model.Analyzers/README.md) - Compile-time validation

## Build

### Normal development - builds with timestamped dev packages

```sh
> dotnet build
> dotnet test
```

This will:

1. Build all projects using project references.
2. Run the tests

### Release

```sh
> dotnet build -c Release
```

This will build the nuget packages. The proejects will be configured to use package references.

### Force package generation in debug

```sh
> dotnet build -p:ForcePackageGeneration=true
```

### Clear local cache when needed

```sh
> dotnet nuget locals all --clear
```

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

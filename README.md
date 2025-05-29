# GraphModel

**Note**: The build is currently broken as the project is going through a major refactoring. GitHub's coee agent cannot work on Pull Requess against branches other than `main` so here we are :( 

Additional context for the project from [Savas Parastatidis](https://savas.me)' blog: [Playing with graphs and neo4j](https://savas.me/2025/05/23/playing-with-graphs-and-neo4j/).

GraphModel is a modular .NET project for working with graph data structures and graph databases. It consists of two main libraries:

- **Graph.Model**: A powerful and flexible abstraction for graph data, supporting type-safe nodes and relationships, LINQ queries, transactions, and provider-agnostic architecture.
- **Graph.Provider.Neo4j**: A high-performance Neo4j backend for Graph.Model, translating LINQ queries to Cypher, managing transactions, and providing seamless integration with Neo4j databases.

## Overview

GraphModel enables developers to model, query, and manipulate graph data in .NET applications with a clean, type-safe API. The core library (`Graph.Model`) defines the abstractions and interfaces for graph operations, while provider packages (such as `Graph.Provider.Neo4j`) implement these abstractions for specific graph databases.

### Key Features

- Strongly-typed nodes and relationships with attribute-based configuration
- LINQ support for expressive and familiar graph queries
- ACID transaction support
- Flexible relationship traversal and loading options
- Provider-agnostic design for easy integration with different graph backends
- Neo4j provider with LINQ-to-Cypher translation, automatic constraint management, and connection pooling

## Getting Started

1. **Install the core library:**

   ```bash
   dotnet add package Cvoya.Graph.Model
   ```

2. **(Optional) Install the Neo4j provider:**

   ```bash
   dotnet add package Cvoya.Graph.Provider.Neo4j
   ```

3. **Define your graph model and use the API as described in the [Graph.Model documentation](src/Graph.Model/README.md).**

For more details, see the individual library READMEs:

- [Graph.Model](src/Graph.Model/README.md)
- [Graph.Provider.Neo4j](src/Graph.Provider.Neo4j/README.md)

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

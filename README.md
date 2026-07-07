# GraphModel

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET](https://img.shields.io/badge/.NET-10.0+-purple.svg)](https://dotnet.microsoft.com/download)
[![GitHub release](https://img.shields.io/github/v/release/cvoya-com/graphmodel-dotnet)](https://github.com/cvoya-com/graphmodel-dotnet/releases)
[![CI](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/ci.yml)
[![Documentation](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/docs.yml/badge.svg)](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/docs.yml)
[![Codecov](https://codecov.io/gh/cvoya-com/graphmodel-dotnet/branch/main/graph/badge.svg)](https://codecov.io/gh/cvoya-com/graphmodel-dotnet)
[![CodeQL](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/codeql.yml/badge.svg)](https://github.com/cvoya-com/graphmodel-dotnet/actions/workflows/codeql.yml)
[![Contributors](https://img.shields.io/github/contributors/cvoya-com/graphmodel-dotnet.svg)](https://github.com/cvoya-com/graphmodel-dotnet/graphs/contributors)
[![Issues](https://img.shields.io/github/issues/cvoya-com/graphmodel-dotnet.svg)](https://github.com/cvoya-com/graphmodel-dotnet/issues)
[![Stars](https://img.shields.io/github/stars/cvoya-com/graphmodel-dotnet.svg)](https://github.com/cvoya-com/graphmodel-dotnet/stargazers)

A powerful, type-safe .NET library ecosystem for working with graph data structures and graph databases. GraphModel provides a clean abstraction layer over graph databases with advanced LINQ querying, transaction management, and relationship traversal capabilities.

## 🚀 Features

- **🔒 Type-Safe Graph Operations** - Work with strongly-typed nodes and relationships using modern C# features
- **🔍 Advanced LINQ Support** - Query your graph using familiar LINQ syntax with graph-specific extensions
- **🔄 Graph Traversal & Path Finding** - Navigate complex relationships with depth control and direction constraints
- **⚡ Transaction Management** - Full ACID transaction support with async/await patterns
- **🎯 Provider Architecture** - Clean abstraction supporting multiple graph database backends
- **📊 Neo4j Integration** - Complete Neo4j implementation with LINQ-to-Cypher translation
- **🛡️ Compile-Time Validation** - Code analyzers ensure that the data model requirements
- **🏗️ Complex Object Serialization** - Automatic handling of complex properties and circular references
- **📈 Build-time code generation** - Automatic code generation for efficient serialization/deserialization of domain data types
- **🎨 Attribute-Based Configuration** - Configure nodes and relationships using intuitive attributes

## 📦 Packages

To get started, you only need to install the Neo4j provider package:

| Package                                   | Description                                             | NuGet                                                                                |
| ----------------------------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `Cvoya.Graph.Model.Neo4j`                 | Neo4j provider implementation (**required**)            | ![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Neo4j.svg)                 |
| `Cvoya.Graph.Model`                       | Core abstractions and interfaces                        | ![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.svg)                       |
| `Cvoya.Graph.Model.Analyzers`             | Compile-time code analyzers (**optional, recommended**) | ![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Analyzers.svg)             |
| `Cvoya.Graph.Model.Serialization.CodeGen` | Compile-time code generation                            | ![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Serialization.CodeGen.svg) |
| `Cvoya.Graph.Model.Serialization`         | Serialization-related functionality                     | ![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Serialization.svg)         |

## 🏃‍♂️ Quick Start

### 1. Installation

```bash
# Install the Neo4j provider (required)
dotnet add package Cvoya.Graph.Model.Neo4j

# Optionally, add code analyzers for extra compile-time validation (recommended)
dotnet add package Cvoya.Graph.Model.Analyzers
```

### 2. Define Your Domain Model

```csharp
using Cvoya.Graph.Model;

[Node("Person")]
public record Person : Node
{
    [Property(Label = "first_name", IsIndexed = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property(Label = "last_name", IsIndexed = true)]
    public string LastName { get; set; } = string.Empty;

    // The Property attribute is optional
    public int Age { get; set; }
    public Address? HomeAddress { get; set; } // Complex types supported
}

// When used as the type of a property, it makes that property a "complex property"
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public Point? Coordinates { get; set; } // Spatial data
}

[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }

    // Relationships cannot have complex properties
}
```

For your convenience, the `Cvoya.Graph.Model` package also offers `Node` and `Relationship` records so that you only have to focus on your domain-specific properties:

```csharp
public record Person : Node
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public Address? HomeAddress { get; set; }
}

public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
}
```

or:

```csharp
public record Knows : Relationship
{
    public Knows() : base(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")) { }
    public Knows(Person p1, Person p2) : base(p1.Id, p2.Id) {}

    public DateTime Since { get; set; }
}
```

### 3. Create Graph Instance

```csharp
using Cvoya.Graph.Model.Neo4j;

// Neo4j provider
var store = new Neo4jGraphStore(
    uri: "bolt://localhost:7687",
    username: "neo4j",
    password: "password",
    databaseName: "myapp"
);
var graph = store.Graph;
```

### 4. Basic Operations

```csharp
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

await graph.CreateNodeAsync(alice);

// Create relationships
var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNodeAsync(bob);

var friendship = new Knows(alice.Id, bob.Id)
{
    Since = DateTime.UtcNow.AddYears(-2)
};
await graph.CreateRelationshipAsync(friendship);

// Query with LINQ
var youngPeople = await graph.Nodes<Person>()
    .Where(p => p.Age < 30)
    .Where(p => p.HomeAddress != null && p.HomeAddress.City == "Portland")
    .OrderBy(p => p.FirstName)
    .ToListAsync();

// Graph traversal
var alicesFriends = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .Traverse<Knows, Person>(minDepth: 1, maxDepth: 2)
    .Where(friend => friend.Age > 20)
    .ToListAsync();
```

### 5. Transaction Management

```csharp
await using var transaction = await graph.GetTransactionAsync();
try
{
    await graph.CreateNodeAsync(person, transaction: transaction);
    await graph.CreateRelationshipAsync(relationship, transaction: transaction);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## 📚 Documentation

- **[API Reference](https://cvoya-com.github.io/graphmodel-dotnet/api/)** - Generated API documentation for all packages
- **[Core Concepts](docs/core-concepts.md)** - Understanding nodes, relationships, and entities
- **[LINQ Querying](docs/querying.md)** - Advanced query patterns and graph traversal
- **[Transaction Management](docs/transactions.md)** - Working with ACID transactions
- **[Attributes & Configuration](docs/attributes.md)** - Customizing nodes and relationships
- **[Best Practices](docs/best-practices.md)** - Performance tips and patterns
- **[Provider Implementers Guide](docs/provider-implementers-guide.md)** - Current provider SPI, storage conventions, and contract-test reuse
- **[Neo4j Provider](src/Graph.Model.Neo4j/README.md)** - Neo4j-specific features and configuration
- **[Code Analyzers](src/Graph.Model.Analyzers/README.md)** - Compile-time validation rules
- **[Code Generation](src/Graph.Model.Serialization.CodeGen/README.md)** - Build-time code generation for serialization
- **[Troubleshooting](docs/troubleshooting.md)** - In case you encounter issues
- **[Building Graph Model](docs/graph-model-developers.md)** - Building the projects in this repository
- **[AI agent documentation](docs/ai-agents.md)** - Where to find context for Claude Code, Codex, Copilot, and other AI coding tools

## 💡 Examples

Explore comprehensive examples in the [`examples/`](examples/) directory:

- **[Basic Serialization](examples/Example0.BasicSerialization/)** - CRUD operations and complex object handling
- **[Basic CRUD](examples/Example1.BasicCRUD/)** - Fundamental create, read, update, delete operations
- **[LINQ & Traversal](examples/Example2.LINQAndTraversal/)** - Advanced querying and graph navigation
- **[Transaction Management](examples/Example3.TransactionManagement/)** - ACID transactions and rollback scenarios
- **[Advanced Scenarios](examples/Example4.AdvancedScenarios/)** - Complex patterns and optimizations
- **[Social Network](examples/Example5.SocialNetwork/)** - Real-world social graph implementation
- **[Full Text Search](examples/Example6.FullTextSearch/)** - Search across nodes and relationships
- **[Simple Movie Example](examples/SimpleMovieExample/)** - Compact movie graph walkthrough

## 🏗️ Building & Testing

### Build Configurations

GraphModel supports multiple build configurations for different scenarios:

```bash
# Development (fastest, project references)
dotnet build --configuration Debug

# Local package testing (test package references before publishing)
dotnet build --configuration LocalFeed

# Production builds (package references, requires VERSION file)
dotnet build --configuration Release
```

For testing package references locally before publishing to NuGet:

```bash
# Method 1: Direct LocalFeed build
dotnet build --configuration LocalFeed
dotnet build --configuration Release

# Method 2: Using helper script
./scripts/setup-local-feed-msbuild.sh
dotnet build --configuration Release

# Cleanup when done
dotnet msbuild -target:CleanLocalFeed
```

See **[Build System Documentation](docs/graph-model-developers.md)** for complete details.

## 🏗️ Architecture

GraphModel follows a clean, layered architecture:

```
┌─────────────────────────────────┐
│          Your Application       │
├─────────────────────────────────┤
│        Graph.Model (Core)       │  ← Abstractions & LINQ
├─────────────────────────────────┤
│     Graph.Model.Neo4j           │  ← Provider Implementation
├─────────────────────────────────┤
│         Neo4j Database          │  ← Storage Layer
└─────────────────────────────────┘
```

**Key Components:**

- **IGraph** - Main entry point for all graph operations
- **INode / IRelationship** - Type-safe entity contracts
- **IGraphQueryable<T>** - LINQ provider with graph-specific extensions
- **IGraphTransaction** - ACID transaction management
- **Attributes** - Declarative configuration (Node, Relationship, Property)

## 🔧 Requirements

- **.NET 10.0** or later
- **Neo4j 4.0+** (5.x recommended for Neo4j provider)
- **C# 14** language features

## 📖 Related Resources

- [Blog Post: GraphModel: A .NET Abstraction for Graphs](https://savas.me/2025/06/27/graphmodel-a-net-abstraction-for-graphs/)
- [Blog Post: Playing with graphs and neo4j](https://savas.me/2025/05/23/playing-with-graphs-and-neo4j/) by [Savas Parastatidis](https://savas.me)
- [Neo4j Documentation](https://neo4j.com/docs/)
- [Graph Database Concepts](https://neo4j.com/developer/graph-database/)

## 🤖 AI agent documentation

This repo provides context for AI coding agents (Claude Code, Codex, Copilot, and others). The canonical instruction set is **[AGENTS.md](AGENTS.md)**; see **[docs/ai-agents.md](docs/ai-agents.md)** for the per-tool map.

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

Special thanks to the [Neo4j](https://neo4j.com/) team for creating an excellent graph database and driver ecosystem that makes this library possible.

---

**Built with ❤️ by [Savas Parastatidis](https://savas.me)**

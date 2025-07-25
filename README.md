# GraphModel

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET](https://img.shields.io/badge/.NET-10.0+-purple.svg)](https://dotnet.microsoft.com/download)
[![GitHub release](https://img.shields.io/github/release/savasp/graphmodel.svg)](https://github.com/savasp/graphmodel-dotnet/releases)
[![Build Status](https://github.com/savasp/graphmodel-dotnet/workflows/🧪%20Tests/badge.svg)](https://github.com/savasp/graphmodel-dotnet/actions/workflows/tests.yml)
[![Coverage](https://github.com/savasp/graphmodel-dotnet/workflows/📊%20Code%20Coverage/badge.svg)](https://github.com/savasp/graphmodel-dotnet/actions/workflows/coverage.yml)
[![Codecov](https://codecov.io/gh/savasp/graphmodel/branch/main/graph/badge.svg)](https://codecov.io/gh/savasp/graphmodel)
[![CodeQL](https://github.com/savasp/graphmodel-dotnet/workflows/🔒%20CodeQL%20Analysis/badge.svg)](https://github.com/savasp/graphmodel-dotnet/actions/workflows/codeql.yml)
[![Neo4j Compatibility](https://github.com/savasp/graphmodel-dotnet/workflows/🗃️%20Neo4j%20Compatibility%20Tests/badge.svg)](https://github.com/savasp/graphmodel-dotnet/actions/workflows/neo4j-compatibility.yml)
[![Contributors](https://img.shields.io/github/contributors/savasp/graphmodel.svg)](https://github.com/savasp/graphmodel-dotnet/graphs/contributors)
[![Issues](https://img.shields.io/github/issues/savasp/graphmodel.svg)](https://github.com/savasp/graphmodel-dotnet/issues)
[![Stars](https://img.shields.io/github/stars/savasp/graphmodel.svg)](https://github.com/savasp/graphmodel-dotnet/stargazers)

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
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("first_name", Index = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name", Index = true)]
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
public class Knows : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

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

public record Knows : Relationship
{
    public DateTime Since { get; set; }
}

// or

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

var friendship = new Knows(alice, bob)
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
    .Traverse<Person, Knows, Person>()
    .WithDepth(1, 2)
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
    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

## 📚 Documentation

- **[Core Concepts](docs/core-concepts.md)** - Understanding nodes, relationships, and entities
- **[LINQ Querying](docs/querying.md)** - Advanced query patterns and graph traversal
- **[Transaction Management](docs/transactions.md)** - Working with ACID transactions
- **[Attributes & Configuration](docs/attributes.md)** - Customizing nodes and relationships
- **[Best Practices](docs/best-practices.md)** - Performance tips and patterns
- **[Neo4j Provider](src/Graph.Model.Neo4j/README.md)** - Neo4j-specific features and configuration
- **[Code Analyzers](src/Graph.Model.Analyzers/README.md)** - Compile-time validation rules
- **[Code Generation](src/Graph.Model.Serialization.CodeGen/README.md)** - Compile-time validation rules
- **[API Reference](docs/api)** - API documentation generated from the source code
- **[Troubleshooting](docs/troubleshooting.md)** - In case you encounter issues
- **[Building Graph Model](docs/graph-model-developers.md)** - Building the projects in this repository

## 💡 Examples

Explore comprehensive examples in the [`examples/`](examples/) directory:

- **[Basic Serialization](examples/Example0.BasicSerialization/)** - CRUD operations and complex object handling
- **[Basic CRUD](examples/Example1.BasicCRUD/)** - Fundamental create, read, update, delete operations
- **[LINQ & Traversal](examples/Example2.LINQAndTraversal/)** - Advanced querying and graph navigation
- **[Transaction Management](examples/Example3.TransactionManagement/)** - ACID transactions and rollback scenarios
- **[Advanced Scenarios](examples/Example4.AdvancedScenarios/)** - Complex patterns and optimizations
- **[Social Network](examples/Example5.SocialNetwork/)** - Real-world social graph implementation

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

See **[Build System Documentation](docs/BUILD_SYSTEM.md)** for complete details.

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
- **C# 12** language features

## 📖 Related Resources

- [Blog Post: GraphModel: A .NET Abstraction for Graphs](https://savas.me/2025/06/27/graphmodel-a-net-abstraction-for-graphs/)
- [Blog Post: Playing with graphs and neo4j](https://savas.me/2025/05/23/playing-with-graphs-and-neo4j/) by [Savas Parastatidis](https://savas.me)
- [Neo4j Documentation](https://neo4j.com/docs/)
- [Graph Database Concepts](https://neo4j.com/developer/graph-database/)

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

# Cvoya.Graph.Model

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**GraphModel** is a powerful, type-safe .NET library for working with graph data structures and graph databases. This core package provides the fundamental abstractions, interfaces, and base implementations.

## 🚀 Quick Start

```csharp
// Define your graph entities
[Node("User")]
public class User : INode
{
    public string Id { get; set; }

    [Property]
    public string Email { get; set; }

    [Property]
    public string Name { get; set; }
}

[Relationship("FOLLOWS")]
public class Follows : IRelationship
{
    public string Id { get; set; }
    public DateTime CreatedDate { get; set; }
}

// Use with a provider (Neo4j, etc.)
var graph = new Neo4jGraph(connectionString);
await graph.CreateNode(new User { Email = "user@example.com", Name = "John" });
```

## 📦 Core Features

- **Type-safe entity modeling** with attributes and interfaces
- **LINQ-style querying** with full IntelliSense support
- **Graph traversal** with depth control and filtering
- **Transaction management** with ACID guarantees
- **Extensible provider model** for different graph databases
- **Compile-time validation** with built-in analyzers

## 🏗️ Architecture

This package provides the core abstractions:

- **`INode`** - Graph node interface
- **`IRelationship`** - Graph relationship interface
- **`IGraph`** - Main graph operations interface
- **`IGraphQueryable<T>`** - LINQ-style querying
- **Attributes** - Entity configuration (`[Node]`, `[Relationship]`, `[Property]`)

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://savasp.github.io/graphmodel/)**

### Key Documentation Sections

- **[Getting Started Guide](https://savasp.github.io/graphmodel/packages/graph-model/getting-started.html)** - Detailed setup and first steps
- **[Core Interfaces](https://savasp.github.io/graphmodel/packages/graph-model/core-interfaces.html)** - Understanding the type system
- **[Querying Guide](https://savasp.github.io/graphmodel/packages/graph-model/querying.html)** - LINQ-style graph queries
- **[Transactions](https://savasp.github.io/graphmodel/packages/graph-model/transactions.html)** - Transaction management
- **[Best Practices](https://savasp.github.io/graphmodel/packages/graph-model/best-practices.html)** - Performance and patterns

## 🔗 Related Packages

- **[Cvoya.Graph.Model.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Model.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Model.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md) and [Code of Conduct](https://github.com/savasp/graphmodel/blob/main/CODE_OF_CONDUCT.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/savasp/graphmodel/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://savasp.github.io/graphmodel/guides/troubleshooting.html) or [open an issue](https://github.com/savasp/graphmodel/issues).

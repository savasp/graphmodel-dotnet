# Cvoya.Graph.Model

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**GraphModel** is a powerful, type-safe .NET library for working with graph data structures and graph databases. This core package provides the fundamental abstractions, interfaces, and base implementations.

## 🚀 Quick Start

```csharp
// Define your graph entities
[Node(Label = "User")]
public record User : Node
{
    public string Email { get; set; }
    public string Name { get; set; }
}

[Relationship(Label = "FOLLOWS")]
public record Follows : Relationship
{
    public DateTime CreatedDate { get; set; }
}

// Use with a provider (Neo4j, etc.)
var store = new Neo4jGraphStore(connectionString);
var graph = store.Graph;
await graph.CreateNodeAsync(new User { Email = "user@example.com", Name = "John" });
```

## 📦 Core Features

- **Type-safe entity modeling** with attributes and interfaces
- **LINQ-style querying** with full IntelliSense support
- **Graph traversal** with depth control and filtering
- **Transaction management** with ACID guarantees
- **Extensible provider model** for different graph databases
- **Compile-time validation** with built-in analyzers
- **Code generation** for performant serialization/deserialization

## 🏗️ Architecture

This package provides the core abstractions:

- **`INode`** - Graph node interface
- **`IRelationship`** - Graph relationship interface
- **`IGraph`** - Main graph operations interface
- **`IGraphQueryable<T>`** - LINQ-style querying
- **Attributes** - Entity configuration (`[Node]`, `[Relationship]`, `[Property]`)

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/savasp/graphmodel-dotnet/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Model.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Model.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Model.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Model.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/savasp/graphmodel-dotnet/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/savasp/graphmodel-dotnet/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/savasp/graphmodel-dotnet/docs/troubleshooting.md) or [open an issue](https://github.com/savasp/graphmodel-dotnet/issues).

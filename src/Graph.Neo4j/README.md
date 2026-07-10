# Cvoya.Graph.Neo4j

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Neo4j.svg)](https://www.nuget.org/packages/Cvoya.Graph.Neo4j/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Neo4j provider** for GraphModel - enables your applications to work with Neo4j graph databases using the GraphModel abstractions and LINQ-style querying.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Neo4j
```

```csharp
using Cvoya.Graph.Neo4j;

// Configure connection
var store = new Neo4jGraphStore("neo4j+s://your-server:7687", "neo4j", "your-password");
var graph = store.Graph;

// Use GraphModel APIs
var users = await (await graph.NodesAsync<User>())
    .Where(u => u.IsActive)
    .OrderBy(u => u.CreatedDate)
    .ToListAsync();
```

## 📦 Core Features

- **Full GraphModel compatibility** - Works seamlessly with all GraphModel abstractions
- **Cypher query generation** - Automatic translation from LINQ to optimized Cypher
- **Connection pooling** - Built-in connection management and pooling
- **Transaction support** - Full ACID transactions with Neo4j
- **Index management** - Automatic index creation from entity attributes
- **Complex type handling** - Serialization of complex properties and collections

## 🏗️ Architecture

This provider implements:

- **`IGraph`** - Main graph operations against Neo4j
- **`IGraphQueryProvider`** - LINQ-to-Cypher query translation
- **`IGraphTransaction`** - Neo4j transaction wrapper
- **Connection management** - Efficient Neo4j driver usage

## 🔧 Configuration

```csharp
var store = new Neo4jGraphStore("neo4j+s://localhost:7687", "neo4j", "password");
var graph = store.Graph;
```

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/cvoya-com/graphmodel-dotnet/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/docs/troubleshooting.md) or [open an issue](https://github.com/cvoya-com/graphmodel-dotnet/issues).

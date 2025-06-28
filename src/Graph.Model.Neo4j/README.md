# Cvoya.Graph.Model.Neo4j

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Neo4j.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Neo4j provider** for GraphModel - enables your applications to work with Neo4j graph databases using the GraphModel abstractions and LINQ-style querying.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Model.Neo4j
```

```csharp
using Cvoya.Graph.Model.Neo4j;

// Configure connection
var graph = new Neo4jGraph(
    connectionString: "neo4j+s://your-server:7687",
    username: "neo4j",
    password: "your-password"
);

// Use GraphModel APIs
var users = await graph.Nodes<User>()
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
var graph = new Neo4jGraph(
    connectionString: "neo4j+s://localhost:7687",
    username: "neo4j",
    password: "password"
);
```

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/savasp/graphmodel/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Model.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Model.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Model.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Model.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/savasp/graphmodel/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/savasp/graphmodel/docs/troubleshooting.md) or [open an issue](https://github.com/savasp/graphmodel/issues).

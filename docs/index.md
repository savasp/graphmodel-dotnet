---
title: GraphModel Documentation
layout: default
---

# GraphModel Documentation

Welcome to the comprehensive documentation for **GraphModel** - a powerful, type-safe .NET library ecosystem for working with graph data structures and graph databases.

## 🚀 Quick Navigation

### 📖 Getting Started

- **[README & Overview](../README.md)** - Project overview, features, and quick start guide
- **[Build System](BUILD_SYSTEM.md)** - Build configurations, version management, and workflows

### 🧠 Core Concepts

- **[Graph Querying - Usage Examples](graph-querying-usage-examples.md)** - Practical examples and patterns
- **[Graph Querying - Interface Recommendations](graph-querying-interface-recommendations.md)** - Best practices for interface design
- **[Graph Querying - Direct Responses](graph-querying-direct-responses.md)** - Working with query results
- **[Handler-Visitor Architecture](Handler-Visitor-Architecture.md)** - Understanding the architectural patterns

### ⚡ Performance & Operations

- **[Performance Guide](performance.md)** - Optimization tips and benchmarking
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions

### 💡 Examples

Comprehensive examples demonstrating real-world usage:

- **[Basic Serialization](../examples/Example0.BasicSerialization/README.md)** - CRUD operations and complex object handling
- **[Basic CRUD Operations](../examples/Example1.BasicCRUD/README.md)** - Fundamental create, read, update, delete operations
- **[LINQ and Traversal](../examples/Example2.LINQAndTraversal/README.md)** - Advanced querying and graph navigation
- **[Transaction Management](../examples/Example3.TransactionManagement/README.md)** - ACID transactions and rollback scenarios
- **[Advanced Scenarios](../examples/Example4.AdvancedScenarios/README.md)** - Complex patterns and optimizations
- **[Social Network Example](../examples/Example5.SocialNetwork/README.md)** - Real-world social graph implementation

### 📚 API Reference

- **[XML Documentation Files](api-xml/)** - Generated from XML documentation comments

## 🏗️ Project Structure

GraphModel consists of several packages working together:

| Package                           | Description                      | Documentation                                                    |
| --------------------------------- | -------------------------------- | ---------------------------------------------------------------- |
| `Cvoya.Graph.Model`               | Core abstractions and interfaces | [XML Documentation](api-xml/Cvoya.Graph.Model.xml)               |
| `Cvoya.Graph.Model.Neo4j`         | Neo4j provider implementation    | [XML Documentation](api-xml/Cvoya.Graph.Model.Neo4j.xml)         |
| `Cvoya.Graph.Model.Serialization` | Object serialization framework   | [XML Documentation](api-xml/Cvoya.Graph.Model.Serialization.xml) |
| `Cvoya.Graph.Model.Analyzers`     | Compile-time code analyzers      | [XML Documentation](api-xml/Cvoya.Graph.Model.Analyzers.xml)     |

## 🔗 External Links

- **[GitHub Repository](https://github.com/savasp/graphmodel)** - Source code and issue tracking
- **[NuGet Packages](https://www.nuget.org/profiles/Cvoya)** - Published packages
- **[Release Notes](https://github.com/savasp/graphmodel/releases)** - Version history and changelogs

---

_This documentation is automatically generated and deployed from the main branch. Found an issue? Please [contribute](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md) or [report it](https://github.com/savasp/graphmodel/issues)._

---
title: GraphModel Documentation
layout: default
---

# GraphModel Documentation

Welcome to the comprehensive documentation for **GraphModel** - a powerful, type-safe .NET library ecosystem for working with graph data structures and graph databases.

## 🚀 Quick Navigation

### 📖 Getting Started

- **[README & Overview]({{ site.baseurl }}/README.html)** - Project overview, features, and quick start guide
- **[Build System]({{ site.baseurl }}/BUILD_SYSTEM.html)** - Build configurations, version management, and workflows

### 📦 Package Documentation

#### Core Package

- **[Graph.Model]({{ site.baseurl }}/packages/graph-model/)** - Core abstractions and interfaces
  - [Getting Started]({{ site.baseurl }}/packages/graph-model/getting-started.html)
  - [Core Interfaces]({{ site.baseurl }}/packages/graph-model/core-interfaces.html)
  - [Querying with LINQ]({{ site.baseurl }}/packages/graph-model/querying.html)
  - [Transactions]({{ site.baseurl }}/packages/graph-model/transactions.html)
  - [Attributes & Configuration]({{ site.baseurl }}/packages/graph-model/attributes.html)
  - [Best Practices]({{ site.baseurl }}/packages/graph-model/best-practices.html)

#### Provider Packages

- **[Graph.Model.Neo4j]({{ site.baseurl }}/packages/neo4j/)** - Neo4j database provider
- **[Graph.Model.Serialization]({{ site.baseurl }}/packages/serialization/)** - Object serialization framework
- **[Graph.Model.Analyzers]({{ site.baseurl }}/packages/analyzers/)** - Compile-time code analyzers

### 🧠 Cross-Project Guides

- **[Performance Guide]({{ site.baseurl }}/guides/performance.html)** - Optimization tips and benchmarking
- **[Graph Querying - Usage Examples]({{ site.baseurl }}/guides/graph-querying-usage-examples.html)** - Practical examples and patterns
- **[Graph Querying - Interface Recommendations]({{ site.baseurl }}/guides/graph-querying-interface-recommendations.html)** - Best practices for interface design
- **[Graph Querying - Direct Responses]({{ site.baseurl }}/guides/graph-querying-direct-responses.html)** - Working with query results
- **[Handler-Visitor Architecture]({{ site.baseurl }}/guides/Handler-Visitor-Architecture.html)** - Understanding the architectural patterns
- **[Troubleshooting]({{ site.baseurl }}/guides/troubleshooting.html)** - Common issues and solutions

### 💡 Examples

Comprehensive examples demonstrating real-world usage:

- **[Basic Serialization]({{ site.baseurl }}/examples/Example0.BasicSerialization/README.html)** - CRUD operations and complex object handling
- **[Basic CRUD Operations]({{ site.baseurl }}/examples/Example1.BasicCRUD/README.html)** - Fundamental create, read, update, delete operations
- **[LINQ and Traversal]({{ site.baseurl }}/examples/Example2.LINQAndTraversal/README.html)** - Advanced querying and graph navigation
- **[Transaction Management]({{ site.baseurl }}/examples/Example3.TransactionManagement/README.html)** - ACID transactions and rollback scenarios
- **[Advanced Scenarios]({{ site.baseurl }}/examples/Example4.AdvancedScenarios/README.html)** - Complex patterns and optimizations
- **[Social Network Example]({{ site.baseurl }}/examples/Example5.SocialNetwork/README.html)** - Real-world social graph implementation

### 📚 API Reference

- **[XML Documentation Files]({{ site.baseurl }}/api/)** - Generated from XML documentation comments

## 🏗️ Project Structure

GraphModel consists of several packages working together:

| Package                           | Description                      | Documentation                                                                                                           |
| --------------------------------- | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Cvoya.Graph.Model`               | Core abstractions and interfaces | [Docs]({{ site.baseurl }}/packages/graph-model/) \| [XML]({{ site.baseurl }}/api/Cvoya.Graph.Model.xml)                 |
| `Cvoya.Graph.Model.Neo4j`         | Neo4j provider implementation    | [Docs]({{ site.baseurl }}/packages/neo4j/) \| [XML]({{ site.baseurl }}/api/Cvoya.Graph.Model.Neo4j.xml)                 |
| `Cvoya.Graph.Model.Serialization` | Object serialization framework   | [Docs]({{ site.baseurl }}/packages/serialization/) \| [XML]({{ site.baseurl }}/api/Cvoya.Graph.Model.Serialization.xml) |
| `Cvoya.Graph.Model.Analyzers`     | Compile-time code analyzers      | [Docs]({{ site.baseurl }}/packages/analyzers/) \| [XML]({{ site.baseurl }}/api/Cvoya.Graph.Model.Analyzers.xml)         |

## 🔗 External Links

- **[GitHub Repository](https://github.com/savasp/graphmodel)** - Source code and issue tracking
- **[NuGet Packages](https://www.nuget.org/profiles/Cvoya)** - Published packages
- **[Release Notes](https://github.com/savasp/graphmodel/releases)** - Version history and changelogs

---

_This documentation is automatically generated and deployed from the main branch. Found an issue? Please [contribute](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md) or [report it](https://github.com/savasp/graphmodel/issues)._

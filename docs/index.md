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

### 🧠 Core Concepts

- **[Graph Querying - Usage Examples]({{ site.baseurl }}/graph-querying-usage-examples.html)** - Practical examples and patterns
- **[Graph Querying - Interface Recommendations]({{ site.baseurl }}/graph-querying-interface-recommendations.html)** - Best practices for interface design
- **[Graph Querying - Direct Responses]({{ site.baseurl }}/graph-querying-direct-responses.html)** - Working with query results
- **[Handler-Visitor Architecture]({{ site.baseurl }}/Handler-Visitor-Architecture.html)** - Understanding the architectural patterns

### ⚡ Performance & Operations

- **[Performance Guide]({{ site.baseurl }}/performance.html)** - Optimization tips and benchmarking
- **[Troubleshooting]({{ site.baseurl }}/troubleshooting.html)** - Common issues and solutions

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

| Package                           | Description                      | Documentation                                                                   |
| --------------------------------- | -------------------------------- | ------------------------------------------------------------------------------- |
| `Cvoya.Graph.Model`               | Core abstractions and interfaces | [XML Documentation]({{ site.baseurl }}/api/Cvoya.Graph.Model.xml)               |
| `Cvoya.Graph.Model.Neo4j`         | Neo4j provider implementation    | [XML Documentation]({{ site.baseurl }}/api/Cvoya.Graph.Model.Neo4j.xml)         |
| `Cvoya.Graph.Model.Serialization` | Object serialization framework   | [XML Documentation]({{ site.baseurl }}/api/Cvoya.Graph.Model.Serialization.xml) |
| `Cvoya.Graph.Model.Analyzers`     | Compile-time code analyzers      | [XML Documentation]({{ site.baseurl }}/api/Cvoya.Graph.Model.Analyzers.xml)     |

## 🔗 External Links

- **[GitHub Repository](https://github.com/savasp/graphmodel)** - Source code and issue tracking
- **[NuGet Packages](https://www.nuget.org/profiles/Cvoya)** - Published packages
- **[Release Notes](https://github.com/savasp/graphmodel/releases)** - Version history and changelogs

---

_This documentation is automatically generated and deployed from the main branch. Found an issue? Please [contribute](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md) or [report it](https://github.com/savasp/graphmodel/issues)._

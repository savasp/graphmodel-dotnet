# Cvoya.Graph.Model.Analyzers

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Analyzers.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Compile-time code analyzers** for GraphModel - provides static analysis and validation of your graph entity models to catch issues early in the development cycle.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Model.Analyzers
```

The analyzers are automatically enabled when you build your project. No additional configuration required!

```csharp
// The analyzers will catch issues like this:
[Node("User")]
public class User : INode
{
    // ❌ GM001: Missing parameterless constructor
    public User(string name) { Name = name; }

    public string Id { get; set; }

    // ❌ GM002: Property must have public getter and setter
    [Property]
    public string Name { get; private set; }

    // ❌ GM004: Invalid property type for node
    [Property]
    public IGraph Graph { get; set; }
}
```

## 📦 Analyzer Rules

| Rule ID   | Description                                 | Severity |
| --------- | ------------------------------------------- | -------- |
| **GM001** | Missing parameterless constructor           | Error    |
| **GM002** | Property must have public accessors         | Error    |
| **GM003** | Property cannot be graph interface type     | Error    |
| **GM004** | Invalid property type for node              | Error    |
| **GM005** | Invalid property type for relationship      | Error    |
| **GM006** | Complex type contains graph interface types | Error    |
| **GM007** | Duplicate property attribute label          | Warning  |
| **GM008** | Duplicate relationship attribute label      | Warning  |
| **GM009** | Duplicate node attribute label              | Warning  |
| **GM010** | Circular reference without nullable         | Warning  |

## 🔧 Configuration

You can customize analyzer behavior in your `.editorconfig`:

```ini
# Disable specific rules
dotnet_diagnostic.GM007.severity = none

# Change severity levels
dotnet_diagnostic.GM010.severity = error

# Configure for specific files
[**/Generated/*.cs]
dotnet_diagnostic.GM001.severity = none
```

## 📋 Rule Details

### GM001: Missing Parameterless Constructor

```csharp
// ❌ Bad
[Node("User")]
public class User : INode
{
    public User(string name) { /* ... */ }
}

// ✅ Good
[Node("User")]
public class User : INode
{
    public User() { }
    public User(string name) : this() { /* ... */ }
}
```

### GM002: Property Must Have Public Accessors

```csharp
// ❌ Bad
[Property]
public string Name { get; private set; }

// ✅ Good
[Property]
public string Name { get; set; }
```

### GM003: Property Cannot Be Graph Interface Type

```csharp
// ❌ Bad
[Property]
public IGraph Graph { get; set; }

[Property]
public INode RelatedNode { get; set; }

// ✅ Good - use relationships instead
public IGraph Graph => /* get from context */;
```

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/cvoya-com/graphmodel-dotnet/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Model.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Model.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Model.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Model.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/docs/troubleshooting.md) or [open an issue](https://github.com/cvoya-com/graphmodel-dotnet/issues).

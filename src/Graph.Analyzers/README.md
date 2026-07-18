**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Analyzers

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Analyzers.svg)](https://www.nuget.org/packages/Cvoya.Graph.Analyzers/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Compile-time code analyzers** for CVOYA graph - provides static analysis and validation of your graph entity models to catch issues early in the development cycle.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Analyzers
```

The analyzers are automatically enabled when you build your project. No additional configuration required!

```csharp
// The analyzers will catch issues like this:
[Node("User")]
public class User : INode
{
    // ❌ CG001: Missing parameterless constructor
    public User(string name) { Name = name; }

    public string Id { get; set; }

    // ❌ CG002: Property must have public getter and setter
    [Property]
    public string Name { get; private set; }

    // ❌ CG004: Invalid property type for node
    [Property]
    public IGraph Graph { get; set; }
}
```

## 📦 Analyzer Rules

| Rule ID   | Description                                                          | Severity |
| --------- | --------------------------------------------------------------------- | -------- |
| **CG001** | Missing parameterless constructor                                      | Error    |
| **CG002** | Property must have public accessors                                    | Error    |
| **CG003** | Property cannot be graph interface type                                 | Error    |
| **CG004** | Invalid property type for node                                          | Error    |
| **CG005** | Invalid property type for relationship                                  | Error    |
| **CG006** | Complex type contains graph interface types                             | Error    |
| **CG007** | Duplicate property attribute label                                      | Error    |
| **CG008** | Duplicate relationship attribute label                                  | Error    |
| **CG009** | Duplicate node attribute label                                          | Error    |
| **CG010** | Circular reference without nullable                                     | Error    |
| **CG011** | Type should inherit from Node/Relationship instead of implementing directly | Warning  |
| **CG012** | [Node]/[Relationship] on a type that doesn't implement the matching interface | Warning  |
| **CG013** | Both [Node] and [Relationship] applied to the same type                 | Error    |
| **CG014** | Graph entity types (INode/IRelationship) must be reference types        | Error    |
| **CG015** | [ComplexProperty] has no effect on the configured property              | Warning  |
| **CG016** | Open generic graph entities are not supported                           | Error    |

## 🔧 Configuration

You can customize analyzer behavior in your `.editorconfig`:

```ini
# Disable specific rules
dotnet_diagnostic.CG007.severity = none

# Change severity levels
dotnet_diagnostic.CG010.severity = error

# Configure for specific files
[**/Generated/*.cs]
dotnet_diagnostic.CG001.severity = none
```

## 📋 Rule Details

### CG001: Missing Parameterless Constructor

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

### CG002: Property Must Have Public Accessors

```csharp
// ❌ Bad
[Property]
public string Name { get; private set; }

// ✅ Good
[Property]
public string Name { get; set; }
```

### CG003: Property Cannot Be Graph Interface Type

```csharp
// ❌ Bad
[Property]
public IGraph Graph { get; set; }

[Property]
public INode RelatedNode { get; set; }

// ✅ Good - use relationships instead
public IGraph Graph => /* get from context */;
```

### CG016: Open Generic Graph Entities Are Not Supported

```csharp
// ❌ Bad - T is unbound where the non-generic serializer would be generated
public record GenericNode<T> : Node;

// ✅ Good - keep the reusable base abstract and expose a closed concrete entity
public abstract record GenericNode<T> : Node;

[Node("StringNode")]
public record StringNode : GenericNode<string>;
```

The serialization generator supports non-generic concrete entities built from closed generic
constructions. It does not generate serializers for concrete open generic entity declarations or
entities nested in an open generic containing type.

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/cvoya-com/graph/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Neo4j/)** - Neo4j database provider
- **[Cvoya.Graph.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/cvoya-com/graph/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/cvoya-com/graph/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/cvoya-com/graph/blob/main/docs/troubleshooting.md) or [open an issue](https://github.com/cvoya-com/graph/issues).

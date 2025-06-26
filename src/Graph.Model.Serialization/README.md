# Cvoya.Graph.Model.Serialization

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Model.Serialization.svg)](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Object serialization framework** for GraphModel - provides efficient serialization and deserialization of complex objects to graph database storage formats.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Model.Serialization
```

```csharp
using Cvoya.Graph.Model.Serialization;

// Register custom serializers
var registry = new EntitySerializerRegistry();
registry.RegisterSerializer<CustomType>(new CustomTypeSerializer());

// Use with GraphModel
var graph = new Neo4jGraph(connectionString, registry);

// Complex objects are automatically serialized
public class User : INode
{
    public string Id { get; set; }

    [Property]
    public Address HomeAddress { get; set; } // Automatically serialized

    [Property]
    public List<ContactInfo> Contacts { get; set; } // Collections supported
}
```

## 📦 Core Features

- **Automatic complex type handling** - Seamless serialization of nested objects
- **Collection support** - Lists, arrays, dictionaries, and custom collections
- **Type safety** - Strongly-typed serialization with compile-time validation
- **Performance optimized** - Efficient serialization with minimal allocations
- **Extensible** - Custom serializers for domain-specific types
- **Schema evolution** - Handles type changes and migrations gracefully

## 🏗️ Architecture

This package provides:

- **`IEntitySerializer<T>`** - Custom serializer interface
- **`EntitySerializerRegistry`** - Centralized serializer management
- **`EntityFactory`** - Object creation and materialization
- **Runtime representation** - Internal object graph representation

## 🔧 Custom Serializers

```csharp
public class MoneySerializer : IEntitySerializer<Money>
{
    public Serialized Serialize(Money value, EntitySerializerRegistry registry)
    {
        return new SimpleValue(new Dictionary<string, object>
        {
            ["amount"] = value.Amount,
            ["currency"] = value.Currency.Code
        });
    }

    public Money Deserialize(Serialized data, EntitySerializerRegistry registry)
    {
        var dict = data.As<Dictionary<string, object>>();
        return new Money(
            (decimal)dict["amount"],
            new Currency((string)dict["currency"])
        );
    }
}

// Register the serializer
registry.RegisterSerializer<Money>(new MoneySerializer());
```

## 📚 Documentation

For comprehensive documentation and examples:

**🌐 [Complete Documentation](https://savasp.github.io/graphmodel/)**

### Key Sections

- **[Serialization Guide](https://savasp.github.io/graphmodel/packages/serialization/)** - Detailed setup and usage
- **[Custom Serializers](https://savasp.github.io/graphmodel/packages/serialization/custom-serializers.html)** - Building custom serializers
- **[Performance](https://savasp.github.io/graphmodel/packages/serialization/performance.html)** - Optimization techniques
- **[Schema Evolution](https://savasp.github.io/graphmodel/packages/serialization/schema-evolution.html)** - Handling type changes

## 🔗 Related Packages

- **[Cvoya.Graph.Model](https://www.nuget.org/packages/Cvoya.Graph.Model/)** - Core abstractions (required)
- **[Cvoya.Graph.Model.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Model.Neo4j/)** - Neo4j provider (uses this package)
- **[Cvoya.Graph.Model.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization.CodeGen/)** - Compile-time code generation

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/savasp/graphmodel/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/savasp/graphmodel/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://savasp.github.io/graphmodel/guides/troubleshooting.html) or [open an issue](https://github.com/savasp/graphmodel/issues).

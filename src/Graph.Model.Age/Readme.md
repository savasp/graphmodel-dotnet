# Graph.Model.Age

**Age provider** for GraphModel - enables your applications to work with Postgres Apachage Age graph databases using the GraphModel abstractions and LINQ-style querying.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.Model.Age
```

```csharp
using Cvoya.Graph.Model.Age;

// Configure connection
var graph = new AgeGraph(
    connectionString: "postgres://your-server:5432",
    username: "your-username",
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
- **Transaction support** - Full ACID transactions with Postgres Apache Age
- **Index management** - Automatic index creation from entity attributes
- **Complex type handling** - Serialization of complex properties and collections

## 🏗️ Architecture

This provider implements:

- **`IGraph`** - Main graph operations against Postgres Apache Age
- **`IGraphQueryProvider`** - LINQ-to-Cypher query translation
- **`IGraphTransaction`** - Postgres Apache Age transaction wrapper
- **Connection management** - Efficient Postgres Apache Age driver usage

## 🔧 Configuration

```csharp
var graph = new AgeGraph(
    connectionString: "postgres://localhost:5432",
    username: "your-username",
    password: "your-password"
);
```

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/savasp/graphmodel-dotnet/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Model.Age](https://www.nuget.org/packages/Cvoya.Graph.Model.Age/)** - Age database provider
- **[Cvoya.Graph.Model.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Model.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Model.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Model.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Model.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/savasp/graphmodel-dotnet/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/savasp/graphmodel-dotnet/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/savasp/graphmodel-dotnet/docs/troubleshooting.md) or [open an issue](https://github.com/savasp/graphmodel-dotnet/issues).

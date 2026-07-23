**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Serialization

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Serialization.svg)](https://www.nuget.org/packages/Cvoya.Graph.Serialization/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

**Provider-neutral serialization and materialization** for CVOYA Graph.

The package also owns provider-neutral result materialization. Providers adapt driver records into immutable `GraphRecord`/`GraphValue` instances and pass them to `GraphResultMaterializer`; complex-property reassembly, polymorphic type resolution, scalar conversion, and path stitching stay shared.

## Provider contract

The package describes mapped entities without inventing a public identity contract. Domain keys are
optional schema metadata, and provider-native node and relationship identity remains private to the
adapter. Nullable scalar and collection properties preserve `null` independently from empty
collections throughout serialization and materialization.

Provider authors normally consume this package together with `Cvoya.Graph` and, for Cypher
providers, `Cvoya.Graph.Cypher`. Application authors typically receive it transitively from a
database provider.

## 📚 Documentation

For comprehensive documentation, examples, and best practices:

**🌐 [Complete Documentation](https://github.com/cvoya-com/graph/)**

## 🔗 Related Packages

- **[Cvoya.Graph.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Serialization/)** - Object serialization framework
- **[Cvoya.Graph.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Serialization.CodeGen/)** - Code generation for performant serialization/deserialization
- **[Cvoya.Graph.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Analyzers/)** - Compile-time code analyzers

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/cvoya-com/graph/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/cvoya-com/graph/blob/main/LICENSE) file for details.

---

**Need help?** Check the [troubleshooting guide](https://github.com/cvoya-com/graph/blob/main/docs/troubleshooting.md) or [open an issue](https://github.com/cvoya-com/graph/issues).

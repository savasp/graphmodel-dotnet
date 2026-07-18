**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Serialization.CodeGen

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.Serialization.CodeGen.svg)](https://www.nuget.org/packages/Cvoya.Graph.Serialization.CodeGen/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

## Quick starting guide

This package is used to automatically generate code for domain-specific types that adhere to the Graph Model.

## Notes

Concrete graph entity roots must not have unbound type parameters or be nested in an open generic
type. Model reusable generic behavior as an abstract base and expose a non-generic concrete entity
from a closed construction, such as `StringNode : GenericNode<string>`. The optional
`Cvoya.Graph.Analyzers` package reports unsupported declarations as CG016.

When building a project that uses this code generator, use this option to persist the generated code.

```sh
> dotnet build -p:EmitCompilerGeneratedFiles=true
```

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

# Cvoya.Graph.CompatibilityTests

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.CompatibilityTests.svg)](https://www.nuget.org/packages/Cvoya.Graph.CompatibilityTests/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

A shippable **compatibility test suite (TCK)** for GraphModel providers. Implement one small SPI,
bind the suite's `I*Tests` interfaces to your provider, and run the same 345+ contract tests every
in-tree provider runs - proving your provider actually behaves the way GraphModel promises.

## 🚀 Quick Start

```bash
dotnet add package Cvoya.Graph.CompatibilityTests
```

```csharp
// 1. Implement the harness SPI against your provider.
public sealed class MyProviderHarness : IGraphProviderTestHarness
{
    public string ProviderName => "MyCompany.GraphModel.MyProvider";
    public CapabilitySet Capabilities => CapabilitySet.All; // or Except(...) for what you don't support
    public ValueTask InitializeAsync() => /* start/connect infrastructure once per test class */;
    public ValueTask DisposeAsync() => /* release it */;
    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken ct) =>
        /* return an IGraph over an empty store */;
}

// 2. Declare one intermediate base class.
public abstract class MyProviderTest(MyProviderHarness harness)
    : CompatibilityTest(harness), IClassFixture<MyProviderHarness>;

// 3. Bind each suite interface - one line per interface.
public class BasicTests(MyProviderHarness h) : MyProviderTest(h), IBasicTests;
public class QueryTests(MyProviderHarness h) : MyProviderTest(h), IQueryTests;
// ... and so on for the rest of the I*Tests interfaces.
```

Run with `GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test --report-trx` for the certifying, guard-armed
run. See the **[Certifying a provider](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/docs/provider-implementers-guide.md#certifying-a-provider)**
chapter for the full workflow, and `examples/CompatibilityTests.SampleHarness` for a compiling
skeleton.

## 📦 What's in the package

- **Harness SPI** - `IGraphProviderTestHarness`, `StoreIsolation`, `GraphProviderUnavailableException`
- **`CompatibilityTest`** - the shared base class that runs the skip/acquire/dispose choreography once
- **`RequiresCapabilityAttribute`** - marks tests/interfaces that need an optional `GraphCapability`
- **`ComplianceGuard`** / **`ComplianceInventory`** - the executed-count guard (`GRAPHMODEL_COMPLIANCE_STRICT=1`) that catches a mis-wired 0-tests-discovered project
- The `I*Tests` interfaces themselves - the actual contract, as default-implemented xUnit test methods

## 🔧 Capabilities

Some suite areas are optional (for example `FullTextSearch`). Declare only what your backing store
actually supports via `CapabilitySet.All.Except(...)`; the suite skips - never fails - tests that
need a capability you haven't declared, with a fixed, parseable skip reason.

## 📚 Documentation

**🌐 [Complete Documentation](https://github.com/cvoya-com/graphmodel-dotnet/)**

## 🔗 Related Packages

- **[Cvoya.Graph](https://www.nuget.org/packages/Cvoya.Graph/)** - Core graph abstractions
- **[Cvoya.Graph.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Neo4j/)** - Neo4j database provider (the suite's in-tree reference implementation)
- **[Cvoya.Graph.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Serialization/)** - Object serialization framework

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/CONTRIBUTING.md).

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](https://github.com/cvoya-com/graphmodel-dotnet/blob/main/LICENSE) file for details.

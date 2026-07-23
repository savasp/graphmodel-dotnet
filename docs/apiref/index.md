# CVOYA graph API Reference

This site contains the generated API reference for the CVOYA graph .NET libraries, produced from the XML documentation
comments in the source code.

## Packages

- **Cvoya.Graph** — provider-neutral core: `IGraph`, `INode`, `IRelationship`, the LINQ querying surface, and attributes.
- **Cvoya.Graph.Neo4j** — the Neo4j provider implementation.
- **Cvoya.Graph.Age** — the PostgreSQL + Apache AGE provider implementation.
- **Cvoya.Graph.InMemory** — the index-free reference provider and application test double.
- **Cvoya.Graph.Cypher** — the shared typed Cypher AST, planner, renderer, and dialect SPI.
- **Cvoya.Graph.Serialization** — the runtime serialization representation shared by providers and code generation.
- **Cvoya.Graph.Serialization.CodeGen** — the incremental source generator that emits entity serializers.
- **Cvoya.Graph.Analyzers** — Roslyn analyzers that flag common mistakes in consumer domain models.
- **Cvoya.Graph.CompatibilityTests** — the provider contract suite and certification harness SPI.

Browse the [API namespaces](api/toc.yml) for full type and member documentation, or return to the
[project documentation](https://oss.cvoya.com/graph/).

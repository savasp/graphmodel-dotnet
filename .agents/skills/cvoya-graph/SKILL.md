---
name: cvoya-graph
description: CVOYA graph .NET library context. Use when working on CVOYA graph, graph model, Neo4j provider, LINQ graph queries, node/relationship types, analyzers, or serialization codegen.
---

# CVOYA graph project skill

## When to use what

- **Analyzers** (`Cvoya.Graph.Analyzers`): Compile-time validation (for example, base class usage and record constructors). Recommend to consumers; implement in `src/Graph.Analyzers/`.
- **Code generation** (`Cvoya.Graph.Serialization.CodeGen`): Build-time serialization/deserialization for domain types. Used by the serialization layer; implement in `src/Graph.Serialization.CodeGen/`.

## Build configurations

| Config | Use |
|--------|-----|
| **Debug** | Day-to-day dev; project references. `dotnet build --configuration Debug` |
| **LocalFeed** | Build local packages with project references. `dotnet build cvoya-graph.sln --configuration LocalFeed` |
| **Release** | Production packages; requires `VERSION` file. `dotnet build --configuration Release` |

Validate the complete local package set and a package-reference build through the single repository orchestrator: `dotnet msbuild eng/PackageValidation.proj -target:Validate`.

## Key locations

- **Core:** `src/Graph/` — `IGraph`, `INode`, `IRelationship`, LINQ, attributes.
- **Neo4j:** `src/Graph.Neo4j/` — provider, LINQ-to-Cypher, transactions.
- **Apache AGE:** `src/Graph.Age/` — PostgreSQL/AGE provider.
- **In-memory:** `src/Graph.InMemory/` — provider reference implementation and fast test double.
- **Cypher:** `src/Graph.Cypher/` — shared typed Cypher AST and validation.
- **Serialization:** `src/Graph.Serialization/`, `src/Graph.Serialization.CodeGen/`.
- **Provider contract suite:** `src/Graph.CompatibilityTests/` — packable TCK, executed through provider test projects.
- **Tests:** `tests/Graph.*` — see the complete project and service matrix in [AGENTS.md](../../../AGENTS.md).

## References

- [Build-system guide](../../../docs/graph-model-developers.md) — build and release.
- [Best practices](../../../docs/best-practices.md) — model design and usage.

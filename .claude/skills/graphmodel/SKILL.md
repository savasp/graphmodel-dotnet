---
name: graphmodel
description: CVOYA graph .NET library context. Use when working on CVOYA graph, graph model, Neo4j provider, LINQ graph queries, node/relationship types, analyzers, or serialization codegen.
---

# CVOYA graph project skill

## When to use what

- **Analyzers** (`Cvoya.Graph.Analyzers`): Compile-time validation (e.g. base class usage, record constructors). Recommend to consumers; implement in `src/Cvoya.Graph.Analyzers/`.
- **Code generation** (`Cvoya.Graph.Serialization.CodeGen`): Build-time serialization/deserialization for domain types. Used by the serialization layer; implement in `src/Cvoya.Graph.Serialization.CodeGen/`.

## Build configurations

| Config | Use |
|--------|-----|
| **Debug** | Day-to-day dev; project references. `dotnet build --configuration Debug` |
| **LocalFeed** | Test package refs before publish; builds and publishes to local NuGet feed. `dotnet build --configuration LocalFeed` then `dotnet build --configuration Release` |
| **Release** | Production packages; requires `VERSION` file. `dotnet build --configuration Release` |

## Key locations

- **Core:** `src/Graph.Model/` — `IGraph`, `INode`, `IRelationship`, LINQ, attributes.
- **Neo4j:** `src/Cvoya.Graph.Neo4j/` — provider, LINQ-to-Cypher, transactions.
- **Serialization:** `src/Cvoya.Graph.Serialization/`, `src/Cvoya.Graph.Serialization.CodeGen/`.
- **Tests:** `tests/Cvoya.Graph.Tests/` (abstract provider contract suite — executes via provider test projects), `tests/Cvoya.Graph.Neo4j.Tests/` (the contract suite bound to Neo4j + provider tests; needs a running Neo4j — `scripts/containers/start-neo4j.sh`), `tests/Cvoya.Graph.Analyzers.Tests/` (no external deps). See [AGENTS.md](AGENTS.md).

## References

- [docs/graph-model-developers.md](docs/graph-model-developers.md) — build and release.
- [docs/best-practices.md](docs/best-practices.md) — model design and usage.

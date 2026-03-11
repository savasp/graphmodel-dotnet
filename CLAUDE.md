# GraphModel — Project context for Claude Code

Type-safe .NET library for graph data and graph databases (Neo4j), with LINQ, transactions, and optional analyzers/codegen. Targets **.NET 10**.

## Build and test

```bash
# Development (project references)
dotnet build --configuration Debug
dotnet test --configuration Debug
```

**Package testing (before publishing):** `dotnet build --configuration LocalFeed` then `dotnet build --configuration Release`. Release builds require a `VERSION` file and the `CreateRelease` MSBuild target. See [docs/graph-model-developers.md](docs/graph-model-developers.md).

## Stack

- .NET 10, C# 12
- NuGet (no npm/pnpm)

## Key locations

| Area | Path |
|------|------|
| Core | `src/Graph.Model/` |
| Neo4j provider | `src/Graph.Model.Neo4j/` |
| Analyzers | `src/Graph.Model.Analyzers/` |
| Serialization | `src/Graph.Model.Serialization` and `src/Graph.Model.Serialization.CodeGen` |
| Examples | `examples/` |
| Unit tests | `tests/Graph.Model.Tests/` |
| Neo4j integration tests | `tests/Graph.Model.Neo4j.Tests/` |

## More detail

- **Build and release:** [docs/graph-model-developers.md](docs/graph-model-developers.md)
- **Core concepts (nodes, relationships, LINQ):** [docs/core-concepts.md](docs/core-concepts.md)
- **Contributing:** [CONTRIBUTING.md](CONTRIBUTING.md)

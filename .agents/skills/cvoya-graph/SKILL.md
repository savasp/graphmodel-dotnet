---
name: cvoya-graph
description: CVOYA graph .NET library context. Use when working on CVOYA graph, graph model, providers, LINQ graph queries, node/relationship types, analyzers, or serialization codegen.
---

# CVOYA graph project skill

Read [AGENTS.md](../../../AGENTS.md) first; it is the canonical source for the current layout, provider/test matrix, conventions, and required validation. Do not copy its project inventory into plans or reports when a behavioral description is enough.

## Architecture

- `src/Graph/` owns provider-neutral graph and query contracts.
- Provider implementations live in `src/Graph.<Provider>/`; shared provider infrastructure remains provider-neutral.
- Serialization and source generation live in the `src/Graph.Serialization*` projects.
- Compile-time consumer diagnostics live in `src/Graph.Analyzers/`.
- Provider contracts live in `src/Graph.CompatibilityTests/` and execute through provider test projects.
- Tests live under `tests/`; use the repository test runner rather than maintaining a second project list.

Use `Debug` for day-to-day work. For the fast/full test behavior, use the `build-and-test` skill. For package-reference validation, run `dotnet msbuild eng/PackageValidation.proj -target:Validate`.

## References

- [Build-system guide](../../../docs/graph-model-developers.md)
- [Best practices](../../../docs/best-practices.md)

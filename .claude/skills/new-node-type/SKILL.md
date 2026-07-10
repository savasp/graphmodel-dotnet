---
name: new-node-type
description: Scaffold a new node type for the graph model, including the class, tests, and serialization support.
user-invocable: true
argument-hint: "<TypeName> [namespace]"
---

# Create a New Node Type

Scaffold a new node type with proper conventions.

## Arguments

- `$1` — The type name (e.g., `Person`, `Organization`)
- `$2` — Optional namespace (defaults to the project's root namespace)

## Steps

1. **Read existing node types** in `src/Graph/` and the examples in `examples/` to understand the conventions (the `Node` base record, `[Node]` / `[Property]` attributes, property patterns).

2. **Create the node class** following the existing pattern:
   - Inherit from the `Node` base record (or implement `INode` if the surrounding code does)
   - Add the `[Node("Label")]` attribute
   - Use C# records, matching the existing codebase
   - Add XML documentation and the Apache 2.0 header

3. **Add tests** in `tests/Cvoya.Graph.Tests/` (the provider-agnostic contract suite — tests there are inherited and executed by provider test projects) following existing test patterns.

4. **Serialization** is handled by the `Cvoya.Graph.Serialization.CodeGen` source generator automatically for types visible to the compilation — verify the generated serializer appears by building.

5. **Build and test** to verify everything compiles:
   ```bash
   dotnet build --configuration Debug
   dotnet test --configuration Debug --no-build   # full run needs Neo4j; see AGENTS.md
   ```

## References

- [AGENTS.md](../../../AGENTS.md) — test-project semantics
- [docs/core-concepts.md](../../../docs/core-concepts.md) — node and relationship model
- [docs/attributes.md](../../../docs/attributes.md) — available attributes
- [docs/best-practices.md](../../../docs/best-practices.md) — design patterns

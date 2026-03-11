---
name: new-node-type
description: Scaffold a new node type for the graph model, including the class, tests, and serialization support.
user-invocable: true
argument-hint: "<TypeName> [namespace]"
---

# Create a New Node Type

Scaffold a new node type with proper conventions.

## Arguments

- `$0` — The type name (e.g., `Person`, `Organization`)
- `$1` — Optional namespace (defaults to the project's root namespace)

## Steps

1. **Read existing node types** in `src/Graph.Model/` to understand the conventions (base class, attributes, property patterns).

2. **Create the node class** following the existing pattern:
   - Inherit from the correct base class
   - Add `[Node]` attribute (or equivalent)
   - Use C# record types if the existing codebase does
   - Add XML documentation

3. **Add unit tests** in `tests/Graph.Model.Tests/` following existing test patterns.

4. **Add serialization support** if the project uses `Graph.Model.Serialization.CodeGen` — check if codegen picks up the new type automatically or needs registration.

5. **Build and test** to verify everything compiles:
   ```bash
   dotnet build --configuration Debug
   dotnet test tests/Graph.Model.Tests/ --configuration Debug --no-build
   ```

## References

- [docs/core-concepts.md](../../docs/core-concepts.md) — node and relationship model
- [docs/attributes.md](../../docs/attributes.md) — available attributes
- [docs/best-practices.md](../../docs/best-practices.md) — design patterns

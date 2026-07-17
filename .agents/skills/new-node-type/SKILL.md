---
name: new-node-type
description: Add a graph node domain type with matching model, serialization, and behavioral coverage.
user-invocable: true
argument-hint: "<TypeName> [namespace]"
---

# Add a Node Type

Add the type in the consumer, example, or test project named by the task. Concrete domain nodes generally do not belong in the provider-neutral library itself.

## Arguments

- `$1` — type name (for example, `Person`)
- `$2` — optional namespace; otherwise follow the target project's namespace

## Steps

1. Read nearby domain types and the current examples to match record, constructor, property, and labeling conventions.
2. Derive from `Node` unless the surrounding model deliberately implements `INode`. Use `[Node]` only when an explicit physical label is needed, and add `[Property]` metadata only when the storage label differs from the CLR property.
3. Add XML documentation and the Apache 2.0 header when the type is public or belongs to repository source code.
4. Add coverage at the behavior's owning layer. Consumer-model coverage stays with that consumer; provider-neutral behavior belongs in the compatibility suite; core-only classification or serialization behavior belongs in the relevant fast tests.
5. Build so the serialization generator processes the type, then use the repository test runner. Run a provider lane only when the change affects provider behavior:

   ```bash
   ./scripts/run-tests.sh --configuration Debug --lane fast --disable-diff-engine
   ```

## References

- [AGENTS.md](../../../AGENTS.md)
- [Core concepts](../../../docs/core-concepts.md)
- [Attributes](../../../docs/attributes.md)
- [Best practices](../../../docs/best-practices.md)

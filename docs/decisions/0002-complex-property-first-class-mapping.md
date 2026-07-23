---
---

# 0002 — Complex (nested-object) properties as first-class graph structure

- **Status:** Accepted (decided by @savasp on #199, 2026-07-09)
- **Date:** 2026-07-09
- **Related:** #84, #85, #199, ADR-0001
- **Related code:** src/Graph.Neo4j/Entities/ComplexPropertyManager.cs,
  src/Graph.Neo4j/Querying/, src/Graph.Serialization/,
  src/Graph.Serialization.CodeGen/

## Context

CVOYA graph's data model goes beyond the native property-graph model: a .NET node/relationship property
can be a *complex object* — a nested POCO/value object, a collection of them, or a polymorphic hierarchy.
Today those are stored as a **hidden, parallel mechanism**: each complex property is decomposed into
provider-private related nodes joined by mangled `__PROPERTY__{name}__` relationships, reconstructed on
read via an unbounded `[rels*1..]` traversal plus APOC (`apoc.coll.toSet`). This is the single largest
source of conditional branching in the query-translation layer (~74 decisions in `CypherQueryVisitor`,
~63 in `CypherQueryBuilder`, ~53 in `CypherResultProcessor`), query-into-nested is capped at one level and
has a null-check inconsistency, and the APOC dependency is a cross-provider liability.

While planning #84 (shared LINQ front-end + two-level IR), @savasp asked whether to align the model with
the native graph model, and required the data-model direction be settled **before** the level-1
`GraphQueryModel` IR is finalized (the IR must either model complex-property navigation as a first-class
concept or deliberately exclude it). #199 analyzes four options (A keep-but-harden, B opaque serialized
value, C explicit modeling, D first-class structural mapping); this ADR records the decision.

## Decision

**Adopt option D: complex-object properties map to first-class, semantically-named graph structure.**

`Person { Address Home; Address Work; }` maps to:

```
(:Person {Name})-[:Home]->(:Address {Street, City})
(:Person {Name})-[:Work]->(:Address {Street, City})
```

- **Visibility:** the `:Address` nodes and `:Home`/`:Work` relationships are first-class — visible,
  queryable, and traversable — not provider-private.
- **Naming:** the relationship type is derived from the property name by convention, **overridable** via
  attribute to avoid collisions with user-defined domain relationships (replacing the collision-avoidance
  role of the `__PROPERTY__` prefix).
- **Identity:** **per-instance value nodes** — each in-memory instance becomes its own node; two owners
  referencing the same in-memory instance produce two nodes (preserves value-object semantics).
  Applications that need shared identity model the value as an explicit domain node and relationship,
  optionally with `[Property(IsKey = true)]`; there is no public provider-identity base type.
- **Round-trip (read-shape driven):** if the target class **declares** the property, the infrastructure
  **auto-loads it recursively** (transparent round-trip preserved), bounded by an explicit **depth/cycle
  guard** (the current `GraphDataModel.DefaultDepthAllowed = 5` + cycle detection become enforced rather
  than incidental); if the class **omits** it (a slim projection type), there is no property to populate
  but the relationship remains navigable by traversal, with a **co-load shortcut** that returns owner +
  related together (reusing the path/projection machinery).
- **Query:** complex member access (`p.Home.City`) lowers to **traversal steps** in the level-1 IR — there
  is **no bespoke complex-property-navigation concept**. Typed LINQ is preserved and extended: arbitrary
  depth works, `p.Home == null` becomes relationship-existence, and complex *collections*
  (`p.Offices.Any(a => …)`) translate to traversal-existence / aggregate predicates (new front-end work,
  in scope for the IR).
- **Removed:** the `__PROPERTY__` mangled edges, hidden nodes, the APOC read reconstruction, and the
  `NeedsComplexProperties` / enable-disable flag machinery threaded through the translation layer.

## Consequences

- **#84's level-1 IR gets simpler**, not more complex: complex-object access is expressed with the same
  traversal + projection nodes the IR already needs. This is why the direction had to be fixed first.
- **Behavior change (snapshots):** complex-property Cypher differs from today's `__PROPERTY__`/APOC output.
  The affected translation snapshots are **intentionally re-baselined** (itemized, one reviewable commit
  each with justification); all non-complex snapshots stay **byte-identical** per the #84 acceptance bar.
- **Cross-cutting rollout.** D spans the write path (`ComplexPropertyManager`), query translation (#84),
  and materialization (#85's wire model). A half-migrated store is internally inconsistent (queries expect
  `:Home` while storage still writes `__PROPERTY__`), so the write-path + query + materialization switch
  must land **cohesively**. **Recommended sequencing (open for confirmation):** #84 delivers the shared IR
  + front-end/planner/renderer with D's query translation; the write-path and materialization flip land
  together with (or immediately adjacent to) #84 so read/write stay consistent; #85's neutral wire model
  then generalizes the materialization. This is the one sequencing item to confirm before the behavioral
  switch.
- **Serialization CodeGen:** complex-property POCOs are generated as node types with relationship mappings
  rather than nested `EntityInfo`; the generator's reachability closure still applies.
- **Stored-data transition:** breaking storage change. The library does not provide an in-place
  migration from `__PROPERTY__` storage. Recreate the graph and reimport through the v1 model, or
  own and validate a provider-specific transformation. `docs/provider-implementers-guide.md`'s
  former `__PROPERTY__` contract is replaced; `docs/migration-0.x.md` documents the boundary.

## Alternatives considered

Full analysis in #199. In brief:
- **A — keep decomposition, harden + de-APOC.** Preserves behavior but keeps a bespoke, hidden code path;
  highest effort for the least model alignment.
- **B — opaque serialized value (JSON/agtype).** Biggest translation reduction, but the sub-objects stop
  being graph structure (no query-into, no traversal), and it is a write-only-ish blob.
- **C — require explicit modeling.** Cleanest, but the harshest DX cut: users hand-declare and hand-wire
  every value object.
- **D — first-class structural mapping (chosen).** Deletes the special case by folding it into traversal +
  projection, keeps transparent authoring and round-trip, gains first-class queryability of the
  sub-entities, and aligns the .NET model with the property-graph model.

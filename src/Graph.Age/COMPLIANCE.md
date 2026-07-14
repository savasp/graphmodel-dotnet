# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | `Cvoya.Graph.CompatibilityTests` 1.0.0-alpha.20251014.0 |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-14 / local strict certifying run; CI uses the same lane |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | Yes | Lowered to a two-phase Postgres text search; see "Full-text search" below. |
| Transactions | Yes | One PostgreSQL connection and transaction per graph transaction. |
| NestedTransactions | No | |
| ComplexPropertyCascade | Yes | Owned value nodes are deleted transactionally. |
| CallSubqueries | No | |
| PatternSizeProjection | No | |
| MultiLabelMatch | Yes | Logical inheritance labels are lowered to AGE-compatible predicates. |
| OrderByEntity | Yes | Entity ordering is lowered to a stable AGE-compatible key. |
| ShortestPath | No | |
| OptionalTraversal | Yes | Optional matches are lowered while preserving owners with absent paths. |

## Full-text search

AGE cannot express full-text matching in its Cypher subset, so the provider lowers it to Postgres
text search as an **expression-level, two-phase id-seeding** step, before the shared Cypher pipeline
runs (`AgeFullTextSearch`, `AgeFullTextSearchRewriter`):

1. **Phase 1** runs a plain-SQL `to_tsvector(...) @@ plainto_tsquery('simple', @query)` query over the
   graph's `CvoyaNode` / `CvoyaRelationship` label table, on the **caller's transaction**, and returns
   the matching entities' public `Id` values. The shared tokenizer first normalizes the raw text to
   the provider-neutral term definition; those terms reach Postgres only through the `@query` bind
   parameter (never `to_tsquery`, never interpolation).
2. The provider **rewrites** each `Search(source, query)` operator to `Where(source, e => ids.Contains(e.Id))`
   and hands the residual, search-free query to the unchanged shared planner and renderer, so aliases,
   projections, paging, and composition all come out right by construction.

Semantics (the contract floor the shared TCK pins):

- **`'simple'` regconfig** — no stemming and no stop-word removal, so matching is case-insensitive,
  whole-token, and multi-term queries match iff every term matches. `'english'` would drift off the
  cross-provider floor (e.g. `Search("the")`).
- **Matched property set** — for a typed entity, exactly that type's `[Property(IncludeInFullTextSearch)]`
  string properties; inheritance is a per-concrete-type disjunct keyed by each type's own
  `inheritance_labels` label, so `SearchNodes<Person>` also matches `Manager` rows. For dynamic
  entities, all string property values except the framework's internal keys. Text on complex-property
  value nodes is **not** part of the owning entity's match set (the per-type label filter excludes those
  rows); untyped/dynamic search has no domain label to filter on, so it may also match value-node rows.
- **`graph.Search()`** runs phase 1 against both physical tables, materializes the matching nodes and
  relationships on the same transaction, and combines them before applying the outer LINQ pipeline.
  Explicit ordering, paging, and terminals therefore operate on the mixed result as a whole.
- **Typed search-as-source** (`Nodes<T>().Search(...).Traverse(...)`) is rewritten before traversal,
  so the id-filtered node source flows through the existing traversal pipeline with direction, depth,
  path shape, filters, projections, ordering, paging, and terminals intact. Mixed `graph.Search(...)`
  results remain non-traversable because they combine nodes and relationships without one typed node scope.
- **GIN acceleration** (#291) — each physical entity table has one coarse, blob-level GIN expression index over
  `to_tsvector('simple', <graph>.age_fulltext_blob(properties))`, where `age_fulltext_blob` is an
  `IMMUTABLE` function (created per graph schema) returning all string values in the blob minus the
  framework's internal keys. Phase-1 SQL AND-s that coarse conjunct (matching the index expression
  verbatim) with the precise per-type predicate; Postgres serves the coarse conjunct from the index and
  rechecks the precise one. Coarse ⊇ precise, so the result set is identical whether or not the index
  exists — dropping it degrades performance, never correctness. The indexes are created at graph
  provisioning and rebuilt by `IGraph.RecreateIndexesAsync`. Write amplification: a 5,000-node bulk insert of
  text-heavy nodes took ~16 ms without the index and ~52 ms with it (~3.2×) on the local AGE container.
- **Id-set limit** — a single search may seed at most 10,000 ids into the residual query (the list rides
  to AGE as one `agtype` parameter blob); a larger match set fails with an actionable `GraphException`.

## Results

| Inventory test methods | Executed | Capability-skipped | Statically skipped | Failed |
|---|---|---|---|---|
| 409 | 378 | 31 | 1 | 0 |

The compatibility inventory contains 409 runnable test methods. For this capability set (which now
declares `FullTextSearch`), `ComplianceInventory.MinimumExecuted(declared)` is 378 methods and the
strict compliance guard passes; the remaining 31 capability skips are all `CallSubqueries` /
`PatternSizeProjection` tests (tracked by #308). Theory data rows make the runtime case count
slightly larger than the method inventory. The suite also contains one statically skipped,
issue-tracked test; the inventory deliberately excludes it, so it is not counted as a capability
skip above. The provider-specific adapter, dialect, SQL-envelope, full-text, and security tests are
also excluded from the table.

Reproduce:

```bash
scripts/containers/start-age.sh
export AGE_CONNECTION_STRING='Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres'
GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test tests/Graph.Age.Tests/Graph.Age.Tests.csproj --configuration Debug
```

Every runtime skip counted in the capability column carries the suite's declared-capability
reason. Any failed case, unexpected dynamic skip, unavailable-store skip under strict mode, or
compliance-guard failure invalidates this report.

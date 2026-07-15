# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | `Cvoya.Graph.CompatibilityTests` 1.0.0-alpha.20251014.0 |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-15 / local strict certifying run; CI uses the same lane |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | Yes | Lowered to a two-phase Postgres text search; see "Full-text search" below. |
| Transactions | Yes | One PostgreSQL connection and transaction per graph transaction. |
| NestedTransactions | No | |
| ComplexPropertyCascade | Yes | Owned value nodes are deleted transactionally. |
| CallSubqueries | Yes | Correlated collection shapes are lowered to grouped matches and projections. |
| PatternSizeProjection | Yes | Pattern counts are lowered to sequential optional matches and grouped counts. |
| MultiLabelMatch | Yes | An AGE AST pass lowers logical inheritance labels to AGE-compatible predicates. |
| OrderByEntity | Yes | An AGE AST pass lowers entity ordering to the stable public `Id` key. |
| ShortestPath | No | |
| OptionalTraversal | Yes | Optional matches are lowered while preserving owners with absent paths. |
| GroupByAggregation | Yes | The shared structured `WITH` plan uses AGE-native grouping and aggregate functions. |

## Structured Cypher lowering

After the shared planner produces a `CypherStatement`, the AGE query adapter runs an ordered
`CypherPassRunner` before rendering. `AgeCorrelatedProjectionPass` replaces AGE-unsupported pattern
comprehensions and `CALL {}` clauses with one correlated match plus grouped `collect`/aggregate
projections — per-projection filters become conditional aggregation so one filtered projection
cannot narrow its siblings, and the anchoring existence filter becomes a grouped row-count guard;
independent pattern counts and `EXISTS`/`COUNT` predicates in `WHERE` become sequential optional
matches, preserving zero-count owners without multiplying sibling counts (AGE parses `EXISTS { }`
and `COUNT { }` but silently matches nothing, so nothing is left for the renderer to emit natively).
Two correlated shapes have no equivalent staging and are rejected at translation time: a filtered
nested grouping and multiple ordered collections in one projection. `AgeLabelPatternPass` then removes node labels and
relationship types from those and other match patterns and adds equivalent `inheritance_labels` predicates;
`AgeClauseOrderPass` moves ordering and paging without parsing rendered Cypher (including the
path-decomposition and aggregate exceptions), while
`AgeTemporalParameterArithmeticPass` unwraps AGE-unsupported temporal constructors and folds
parameter-only duration arithmetic into bound values. `AgeEntityProjectionPass` expands entity
hydration into typed match, predicate, projection, and ordering clauses, then structurally lowers
named optional paths, `ALL`/`reduce` compatibility, temporal members, string containment, path
indexes, reserved aliases, and empty sums. `AgeInlineComplexPropertyProjectionPass` expands inline
node hydration into typed optional matches, list comprehensions, and collection clauses before the
entity pass applies AGE compatibility lowering. `AgeQueryRunner` no longer performs any of #293's
compatibility rewrites after rendering; these passes preserve the former query semantics without
parsing rendered text.

Scalar-key grouped aggregation needs no AGE-specific grouping pass: the shared planner emits a
structured `WITH` stage whose non-aggregate key establishes AGE's implicit group, with aggregate
columns for `Count`, `Sum`, `Average`, `Min`, and `Max`; key-only projections use `WITH DISTINCT`.
The normal AGE label, alias, and empty-sum passes operate on that AST before rendering. Unsupported
grouping shapes retain the provider-neutral validation message and fail before execution.

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
| 410 | 410 | 0 | 1 | 0 |

The compatibility inventory contains 410 runnable test methods. For this capability set (which now
also declares `GroupByAggregation`), `ComplianceInventory.MinimumExecuted(declared)` is 410 methods
and the strict compliance guard passes with no capability skips.
Theory data rows make the runtime case count
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

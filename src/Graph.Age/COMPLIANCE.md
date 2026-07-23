# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | Repository build (`Cvoya.Graph.CompatibilityTests` 0.0.0-dev) |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-22 / local strict certifying run; CI uses the same lane |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | Yes | PostgreSQL discovers native/external tables; residual Cypher correlates private graphids. |
| Transactions | Yes | One PostgreSQL connection and transaction per graph transaction. |
| NestedTransactions | No | |
| ComplexPropertyCascade | Yes | Owned value nodes are deleted transactionally. |
| CallSubqueries | Yes | Correlated collection shapes are lowered to grouped matches and projections. |
| PatternSizeProjection | Yes | Pattern counts are lowered to sequential optional matches and grouped counts. |
| MultiLabelMatch | Yes | An AGE AST pass lowers logical inheritance labels to AGE-compatible predicates. |
| LabelFiltering | Yes | Caller-supplied labels lower to escaped literal membership tests over physical and logical inheritance labels. |
| OrderByEntity | No | Scalar property ordering is supported; bare whole-entity ordering is rejected rather than rewritten to public `Id`. |
| ShortestPath | No | Deliberately unsupported for v1.0; the capability-gated shortest-path contract skips for AGE. #355 tracks any future implementation. |
| OptionalTraversal | Yes | Optional matches are lowered while preserving owners with absent paths. |
| GroupByAggregation | Yes | The shared structured `WITH` plan uses AGE-native grouping and aggregate functions. |
| RelationshipPredicates | Yes | Per-hop predicates use indexed list filtering; anchored existence uses optional-match count stages. |
| SetOperations | Yes | Every nested `Union`/`Concat` branch runs through the complete AGE lowering pipeline with disjoint parameters and normalized projection aliases. |
| NullElementsInSimpleCollections | Yes | Typed storage metadata preserves element type and null positions without entering public property bags. |

## Structured Cypher lowering

After the shared planner produces a `CypherStatement`, the AGE query adapter recursively lowers every
`SetOperationClause` branch, including nested and chained trees, through the same ordered
`CypherPassRunner` before rendering. Branch parameter namespaces remain disjoint; temporal parameters
added during lowering use the first free provider-local name. `AgeRelationshipPredicatePass` replaces
AGE-unsupported universal relationship predicates with indexed list filtering so every expanded hop
must satisfy the caller predicate.

`AgeCorrelatedProjectionPass` replaces AGE-unsupported pattern
comprehensions and `CALL {}` clauses with one correlated match plus grouped `collect`/aggregate
projections — per-projection filters become conditional aggregation so one filtered projection
cannot narrow its siblings, and the anchoring existence filter becomes a grouped row-count guard;
independent pattern counts and `EXISTS`/`COUNT` predicates in `WHERE` become sequential optional
matches, preserving zero-count owners without multiplying sibling counts (AGE parses `EXISTS { }`
and `COUNT { }` but silently matches nothing, so nothing is left for the renderer to emit natively).
Two correlated shapes have no equivalent staging and are rejected at translation time: a filtered
nested grouping and multiple ordered collections in one projection. `AgeLabelPatternPass` then removes node labels and
relationship types from those and other match patterns and adds native `labels()` / `type()` predicates
with a provider-reserved `inheritance_labels` alternative for mapped names outside AGE's native
grammar, while excluding provider-owned complex values;
`AgeClauseOrderPass` moves ordering and paging without parsing rendered Cypher (including the
path-decomposition and aggregate exceptions), while
`AgeTemporalParameterArithmeticPass` unwraps AGE-unsupported temporal constructors, folds
parameter-only duration arithmetic into bound values, and shifts stored temporal arithmetic across
comparisons into the opposing bound. `AgeEntityProjectionPass` expands entity
hydration into typed match, predicate, projection, and ordering clauses, then structurally lowers
named optional paths, `ALL`/`reduce` compatibility, temporal members, string containment, path
indexes, reserved aliases, and empty sums. `AgeInlineComplexPropertyProjectionPass` expands inline
node hydration into typed optional matches, list comprehensions, and collection clauses before the
entity pass applies AGE compatibility lowering. `AgeSetOperationProjectionPass` assigns every leaf
projection one compatible alias shape before rendering. `AgeQueryRunner` performs no textual
compatibility rewrites after rendering.

Scalar-key grouped aggregation needs no AGE-specific grouping pass: the shared planner emits a
structured `WITH` stage whose non-aggregate key establishes AGE's implicit group, with aggregate
columns for `Count`, `Sum`, `Average`, `Min`, and `Max`; key-only projections use `WITH DISTINCT`.
The normal AGE label, alias, and empty-sum passes operate on that AST before rendering. Unsupported
grouping shapes retain the provider-neutral validation message and fail before execution.

## Atomic subgraph creation

`CreateAsync(source, relationship, target, options)` sends its existence and schema-uniqueness
probes, conditional endpoint writes, breadth-first complex-property levels, relationship write, and
transient-marker cleanup through one `NpgsqlBatch.ExecuteReaderAsync` call. Value-node and
complex-property relationship IDs are assigned before execution, so no batch command depends on a
client read from an earlier result set. Create-only mode gates both roots together; create-missing
mode gates each root independently and never updates or extends a matched endpoint. Failures found
in later result sets roll back the provider-owned transaction or the operation savepoint inside a
caller-owned transaction. This shape is covered against the pinned Apache AGE 1.7.0 / PostgreSQL 18
runtime in both endpoint modes, with nested complex properties on both endpoints and an internal
execution-boundary counter asserting one batch round-trip.

## Native storage and commands

New user roots use their mapped AGE label/type. The label-lowering pass matches either native
`labels()` / `type()` or the provider-reserved representation for mapped names outside AGE's native
grammar, while concrete external rows need no CVOYA metadata. Graph creation provisions only the
graph; an authorized write discovers or creates only the native label table it needs under the
graph-scoped advisory lock. Read transactions verify graph existence and perform no DDL.

The packaged provider-boundary contract seeds a keyless node/relationship fixture through native
AGE commands and reads it back through typed nodes, dynamic nodes, typed/dynamic relationship
roots, and oriented path segments. Exact public property bags exclude graphids and provider marker
metadata. A before/after inventory of graph labels, indexes, constraints, and graph-schema
functions proves the read path creates no store artifacts.

Full-text discovery covers native logical and genuinely external tables and excludes the reserved
`CvoyaNode` / `CvoyaRelationship` tables from root search. Registered labels use their declared
included properties; the global all-string fallback applies only to unregistered external labels.
Search uses function-free SQL and correlates native graphids without exposing them as model data.

Set mutations select, deduplicate, and freeze native graphids before writes. Node/relationship
updates and deletes, uniqueness self-exclusion, and exact endpoint-intent creation use those graphids
inside one transaction. All-new endpoint creation distinguishes value-equal nodes from an explicit
self-loop, and caller-owned multi-statement operations roll back to an operation savepoint on any
validation, cancellation, or database failure.

## Results

| Inventory test methods | Executed | Capability-skipped | Statically skipped | Failed |
|---|---|---|---|---|
| 501 | 499 | 2 | 0 | 0 |

The compatibility inventory contains 501 runnable test methods. For this capability set,
`ComplianceInventory.MinimumExecuted(declared)` is 499 methods and the strict compliance guard
passes with 2 expected skips: one for undeclared `ShortestPath` and one for undeclared
`OrderByEntity`. The retained provider run reports 727 runtime cases: 725 passed, 2 capability
skipped, and 0 failed.
The provider-neutral shortest-path contract remains in `IQueryTraversalTests` and pins one/all
shortest-path selection, endpoint direction, no-path, and same-node semantics; AGE skips that
contract until AGE 1.8 supplies the native capability tracked by #355. Reachable shortest-path use
is rejected before execution with `GraphQueryTranslationException`; `AgeShortestPathTranslationTests`
pins that deterministic rejection. Theory data rows make the runtime case count larger than the
method inventory. The provider-specific adapter,
dialect, SQL-envelope, full-text, and security tests are excluded from the table.

Reproduce:

```bash
./scripts/containers/start-age.sh
export AGE_CONNECTION_STRING='Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres'
GRAPHMODEL_COMPLIANCE_STRICT=1 ./scripts/run-tests.sh --configuration Debug --lane age \
  --disable-diff-engine --report-trx
```

Every runtime skip counted in the capability column carries the suite's declared-capability
reason. Any failed case, unexpected dynamic skip, unavailable-store skip under strict mode, or
compliance-guard failure invalidates this report.

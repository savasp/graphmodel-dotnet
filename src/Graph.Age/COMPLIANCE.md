# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | Repository build (`Cvoya.Graph.CompatibilityTests` 0.0.0-dev) |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-21 / local strict certifying run; CI uses the same lane |

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
| ShortestPath | No | |
| OptionalTraversal | Yes | Optional matches are lowered while preserving owners with absent paths. |
| GroupByAggregation | Yes | The shared structured `WITH` plan uses AGE-native grouping and aggregate functions. |
| RelationshipPredicates | No | AGE declines variable-path relationship predicates and anchored relationship-existence patterns at translation time. |
| SetOperations | No | AGE rejects typed `Union` and `Concat` at translation time. |

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
relationship types from those and other match patterns and adds native `labels()` / `type()` predicates
with a provider-reserved `inheritance_labels` alternative for mapped names outside AGE's native
grammar, while excluding provider-owned complex values;
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
| 465 | 456 | 9 | 0 | 0 |

The compatibility inventory contains 465 runnable test methods. For this capability set,
`ComplianceInventory.MinimumExecuted(declared)` is 456 methods and the strict compliance guard
confirms all 456 expected method identities with 9 expected skips for undeclared capabilities:
`RelationshipPredicates`, `ShortestPath`, `SetOperations`, and `OrderByEntity`. Theory data rows
make the runtime case count slightly larger than the method inventory but do not increase method
coverage. The provider-specific adapter, dialect, SQL-envelope, full-text, and security tests are
excluded from the table.

Reproduce:

```bash
scripts/containers/start-age.sh
export AGE_CONNECTION_STRING='Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres'
DiffEngine_Disabled=true GRAPHMODEL_COMPLIANCE_STRICT=1 \
  dotnet test tests/Graph.Age.Tests/Graph.Age.Tests.csproj --configuration Debug
```

Every runtime skip counted in the capability column carries the suite's declared-capability
reason. Any failed case, unexpected dynamic skip, unavailable-store skip under strict mode, or
compliance-guard failure invalidates this report.

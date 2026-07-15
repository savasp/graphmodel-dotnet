<p>
  <a href="https://cvoya.com"><img src="https://cvoya.com/images/cvoya-logo-dark-blue.png" alt="CVOYA" width="160"></a>
</p>

**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Age

`Cvoya.Graph.Age` is the PostgreSQL + Apache AGE provider for CVOYA graph. It uses the shared
`Cvoya.Graph.Cypher` dialect SPI and exposes the same typed graph, LINQ, serialization, and
transaction APIs as the other providers.

## Install

```bash
dotnet add package Cvoya.Graph.Age
```

The PostgreSQL database must have the Apache AGE extension installed. The provider loads AGE and
sets the required `ag_catalog` search path on every pooled connection.

## Connect

```csharp
using Cvoya.Graph.Age;

await using var store = new AgeGraphStore(
    "Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres",
    graphName: "my_graph");

await store.CreateGraphIfNotExistsAsync();
var graph = store.Graph;

var activeUsers = await graph.Nodes<User>()
    .Where(user => user.IsActive)
    .ToListAsync();
```

Production applications should supply the connection string directly or through
`AGE_CONNECTION_STRING`; the provider deliberately has no fallback credentials. `AGE_GRAPH`
selects the default graph name when the constructor does not specify one.

An application that already owns its connection pool can pass an AGE-enabled
`NpgsqlDataSource`. In that form, disposing the store does not dispose the caller's data source.

## Supported surface

- typed and dynamic node/relationship CRUD;
- shared LINQ-to-Cypher translation and streaming queries;
- ACID transactions on a dedicated PostgreSQL connection;
- atomic endpoint–relationship–endpoint creation in one Npgsql batch round-trip, including nested
  complex-property subtrees and create-missing endpoint semantics;
- complex-property persistence and cascading cleanup;
- inheritance queries through one physical AGE label plus `inheritance_labels`;
- full-text search, lowered to a two-phase Postgres text-search query (see below);
- scalar-key grouped aggregation through AGE-native `WITH` grouping and aggregate functions;
- correlated collection projections and relationship/complex-collection counts, lowered to
  AGE-supported grouped matches (see below); and
- agtype adaptation for vertices, edges, paths, maps, arrays, large integers, decimals, and
  ISO-8601 temporal values.

The provider does not declare nested transactions or shortest path. Unsupported operations fail
during translation or are capability-skipped by the provider compatibility suite.

### Scalar-key grouped aggregation

The shared planner represents scalar-key `GroupBy` as a structured `WITH` stage: the non-aggregate
key establishes AGE's implicit grouping key, while `Count`, `Sum`, `Average`, `Min`, and `Max`
become aggregate columns consumed by the final projection. Key-only projections use `WITH DISTINCT`.
The existing AGE AST passes then lower labels, reserved aliases, and empty sums without inspecting or
rewriting rendered Cypher. The supported and rejected shapes therefore match the provider-neutral
`GroupByAggregation` contract; unsupported entity keys, element selectors, collection projections,
and filters inside a group fail before database execution with the shared actionable diagnostic.

### Correlated collections and pattern counts

Apache AGE 1.7 cannot execute Cypher pattern comprehensions or `CALL {}` subqueries, and it parses
`EXISTS { ... }` / `COUNT { ... }` pattern subqueries but silently matches nothing. The provider
implements the corresponding shared capabilities through an AGE-local structured AST pass before
rendering. Correlated collection queries match the traversal once and group by its source node,
using `collect`, conditional `count`, ordinary aggregates, and ordered/nested `WITH` stages as
required; a filter inside one projection (`group.Where(...).Average(...)`) becomes conditional
aggregation so it cannot narrow the rows sibling projections aggregate over, and a segment filter
becomes a grouped row-count guard so owners with no qualifying rows produce no result row.
Independent relationship-degree and complex-collection counts — in projections, orderings, and
`Where` filters (including `.Any(...)` existence checks) — use sequential `OPTIONAL MATCH` stages so
owners with no match return zero and multiple counts in one projection do not multiply each other.
Two correlated shapes have no equivalent staging and fail at translation time instead: a filtered
nested grouping and multiple ordered collections in one projection. The resulting nested lists and
maps continue through the provider-neutral result wire model and shared materializer.

### Full-text search

AGE has no full-text operator in its Cypher subset, so `Search(...)` is lowered to Postgres text
search *before* the shared Cypher pipeline runs: phase 1 runs a `to_tsvector(...) @@
plainto_tsquery('simple', @query)` query over the graph's label table on the caller's transaction
and collects the matching entities' `Id`s; the provider then rewrites `Search(source, query)` to
`Where(source, e => ids.Contains(e.Id))` and lets the unchanged planner and renderer serve the
residual query. Matching is case-insensitive, whole-token, and multi-term-AND (the `'simple'`
regconfig sits on the cross-provider contract floor); terms are normalized by the shared tokenizer
and always sent as a bind parameter, never interpolated. Mixed `graph.Search()` queries scan both
physical tables and combine nodes and relationships before applying ordering, paging, or terminals.
A single search may seed at most 10,000 ids (a larger match set fails with an actionable error).
Phase 1 is accelerated by one coarse, blob-level GIN index on each physical table (created at graph
provisioning and rebuilt by `RecreateIndexesAsync`); dropping an index degrades performance but never
correctness. See [COMPLIANCE.md](COMPLIANCE.md) for the full semantics.

## Local AGE

From the repository root:

```bash
scripts/containers/start-age.sh
export AGE_CONNECTION_STRING='Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres'
dotnet test tests/Graph.Age.Tests/Graph.Age.Tests.csproj --configuration Debug
```

The implementation incorporates and substantially reworks Apache AGE provider ideas from
[@paule96's PR #66](https://github.com/cvoya-com/graph/pull/66). In particular, the connection
bootstrap, agtype conversion, SQL envelope, transaction, serialization, and CRUD designs informed
this provider; the dialect-SPI implementation and security hardening were performed as part of
issue #86.

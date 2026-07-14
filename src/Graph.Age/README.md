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
- complex-property persistence and cascading cleanup;
- inheritance queries through one physical AGE label plus `inheritance_labels`;
- full-text search, lowered to a two-phase Postgres text-search query (see below); and
- agtype adaptation for vertices, edges, paths, maps, arrays, large integers, decimals, and
  ISO-8601 temporal values.

The provider does not declare nested transactions, call subqueries, pattern-size projections, or
shortest path. Unsupported operations fail during translation or are capability-skipped by the
provider compatibility suite.

### Full-text search

AGE has no full-text operator in its Cypher subset, so `Search(...)` is lowered to Postgres text
search *before* the shared Cypher pipeline runs: phase 1 runs a `to_tsvector(...) @@
plainto_tsquery('simple', @query)` query over the graph's label table on the caller's transaction
and collects the matching entities' `Id`s; the provider then rewrites `Search(source, query)` to
`Where(source, e => ids.Contains(e.Id))` and lets the unchanged planner and renderer serve the
residual query. Matching is case-insensitive, whole-token, and multi-term-AND (the `'simple'`
regconfig sits on the cross-provider contract floor); the raw query is always a bind parameter, never
interpolated. A single search may seed at most 10,000 ids (a larger match set fails with an actionable
error). Phase 1 is accelerated by a coarse, blob-level GIN index per label table (created at graph
provisioning and by `RecreateIndexesAsync`); dropping the index degrades performance but never
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

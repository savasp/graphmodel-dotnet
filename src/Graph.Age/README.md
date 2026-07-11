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
- inheritance queries through one physical AGE label plus `inheritance_labels`; and
- agtype adaptation for vertices, edges, paths, maps, arrays, large integers, decimals, and
  ISO-8601 temporal values.

The initial provider does not declare full-text search, nested transactions, call subqueries,
pattern-size projections, shortest path, or native multi-label matching. Unsupported operations
fail during translation or are capability-skipped by the provider compatibility suite.

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

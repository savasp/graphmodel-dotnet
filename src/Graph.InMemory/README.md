# Cvoya.Graph.InMemory

An in-process, in-memory provider for the CVOYA graph abstraction. It exists for two audiences:

- **Application developers** who want a fast unit-test double: test business logic against
  `IGraph` with no database and no containers.
- **Provider implementers** who want an executable specification: this provider passes the
  `Cvoya.Graph.CompatibilityTests` suite by interpreting the shared provider-neutral query model
  (`GraphQueryModel`) with LINQ-to-objects — no query language anywhere — which is the working
  proof that the level-1 query model is dialect-free.

## Usage

```csharp
using Cvoya.Graph.InMemory;

await using var store = new InMemoryGraphStore();
var graph = store.Graph;

await graph.CreateNodeAsync(new Person { Name = "Alice" });
var alice = await graph.Nodes<Person>().Where(p => p.Name == "Alice").SingleAsync();
```

Everything on `IGraph` behaves per the public contract: CRUD, LINQ querying (including
traversals and path segments), transactions, streaming, and cancellation. Complex properties are
stored decomposed as owned value nodes linked by property-named relationships, exactly like the
Neo4j provider, so cascade and hydration semantics are real, not object aliasing. Entities
round-trip through the serialization layer on every write and read: mutating an entity you hold
never mutates the store.

## Not a production store

- Data lives in process memory; nothing is persisted.
- Queries are unindexed scans; there is no query optimization.
- Concurrency model: transactions buffer their writes and commit atomically under a single
  store-wide lock (single-writer serialized commits; reads take lock-free snapshots). This is
  simple and correct for a test double, not a throughput design.
- Full-text search (`GraphCapability.FullTextSearch`) is not supported: the `Search*` entry
  points build queryables, and execution fails with a `GraphException`. The compatibility suite
  skips those tests via the capability declaration.

See `docs/testing-with-the-in-memory-provider.md` in the repository for the full guide.

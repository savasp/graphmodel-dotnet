---
---

# Testing your app against the in-memory provider

`Cvoya.Graph.InMemory` is an in-process provider for the CVOYA graph abstraction, built for
testing: your application code talks to `IGraph` exactly as it does in production, but the data
lives in process memory. No database, no containers, no cleanup between test runs.

It is also the abstraction's reference implementation: it passes the
`Cvoya.Graph.CompatibilityTests` suite (the same provider TCK the Neo4j provider is certified
against) by interpreting the shared provider-neutral query model with LINQ-to-objects. If your
code works against the in-memory provider, it is exercising the same public contract every
compliant provider implements.

## Getting started

Add the package (or project reference) and create a store:

```csharp
using Cvoya.Graph;
using Cvoya.Graph.InMemory;

await using var store = new InMemoryGraphStore();
IGraph graph = store.Graph;
```

Everything else is the ordinary `IGraph` surface:

```csharp
var alice = new Person { Name = "Alice" };
var bob = new Person { Name = "Bob" };
await graph.CreateNodeAsync(alice);
await graph.CreateNodeAsync(bob);
await graph.CreateRelationshipAsync(new Knows(alice.Id, bob.Id));

var friends = await graph.Nodes<Person>()
    .Where(p => p.Name == "Alice")
    .Traverse<Knows, Person>()
    .ToListAsync();
```

Your domain types need the same setup they need for any provider: inherit from `Node` /
`Relationship`, and make sure the project that defines them references the
`Cvoya.Graph.Serialization.CodeGen` source generator (it ships inside the provider packages), so
entity serializers are generated at build time.

## A test fixture in a few lines

```csharp
public sealed class GraphFixture : IAsyncLifetime
{
    private readonly InMemoryGraphStore store = new();

    public IGraph Graph => store.Graph;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await store.DisposeAsync();
}
```

For a shared store across tests in a class, call `store.ClearAsync()` between tests instead of
creating a new store.

## What behaves exactly like a real provider

- **CRUD contracts**: `EntityNotFoundException` for missing entities, `GraphException` for
  constraint violations (duplicate ids, missing relationship endpoints, unique/key property
  constraints), `ArgumentException` for invalid input.
- **Reference isolation**: entities round-trip through the serialization layer on every write
  and read. Mutating an object you hold never changes what is stored; two reads return two
  independent object graphs.
- **Complex properties**: stored as decomposed owned value nodes linked by property-named
  relationships (per ADR-0002), so create/update/delete cascade and hydration semantics are the
  real thing.
- **Transactions**: buffered writes with read-your-writes, atomic commit, rollback/dispose
  discards, use-after-completion throws `GraphException`, and disjoint concurrent writes are
  replayed without replacing the latest committed snapshot.
- **LINQ querying**: `Where`/`Select`/ordering/paging/aggregates, traversals
  (`Traverse`, `PathSegments`, `TraversePaths` with depth and direction options), and streaming
  via `await foreach` — all executed by interpreting the same query model the Cypher pipeline
  consumes, with the terminal-operator semantics the compatibility suite pins.
- **Cancellation**: pre-cancelled tokens throw `OperationCanceledException` before touching
  state, on every entry point.

## What is different

- **No full-text search.** The provider does not declare `GraphCapability.FullTextSearch`;
  `Search`/`SearchNodes`/`SearchRelationships` build queryables whose execution throws a
  `GraphException`. The compatibility suite skips those tests with a capability reason.
- **Not a production store.** Nothing is persisted, queries are unindexed scans, and commits are
  serialized through a single store-wide lock. It is a test double and executable specification,
  not a database.

## Certification

The provider's compatibility binding lives in `tests/Graph.InMemory.Tests`: an
`InMemoryHarness` implementing the suite's `IGraphProviderTestHarness`, plus one binding class
per suite interface. Run it like any test project — it needs no infrastructure:

```bash
dotnet test tests/Graph.InMemory.Tests --configuration Debug
```

See the [provider implementers guide](provider-implementers-guide.md#certifying-a-provider) for
what the numbers mean.

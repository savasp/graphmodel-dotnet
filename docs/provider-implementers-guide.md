# Provider Implementers Guide

This guide documents the current provider contract. It describes what a provider must implement today and the storage/query conventions that must remain compatible across providers.

Future provider work is tracked separately: the shared `GraphQueryModel` layer is #84 and dialect capabilities are #85. Reusable provider certification (#95) has landed - see [Certifying a provider](#certifying-a-provider).

## Public SPI

A provider exposes a store type that owns database connectivity and returns an `IGraph` instance. The in-tree provider uses `Neo4jGraphStore` as the public type and keeps `Neo4jGraph` internal. New providers should follow the same shape: one public `XGraphStore` type, optional provider-specific extension methods, and an internal `IGraph` implementation.

The public interfaces to implement are:

- `IGraph` in `src/Graph.Model/IGraph.cs`: CRUD, synchronous query roots, full-text search, schema/index provisioning, and transactions.
- `IGraphTransaction` in `src/Graph.Model/IGraphTransaction.cs`: `CommitAsync()`, `RollbackAsync()`, and `IAsyncDisposable`.
- `IGraphQueryProvider` in `src/Graph.Model/GraphQueryable/IGraphQueryProvider.cs`: expression execution, query creation, and async terminal execution.
- `IGraphQueryable<T>` in `src/Graph.Model/GraphQueryable/`: the single LINQ root returned by `IGraph.Nodes<N>`/`Relationships<R>`/`DynamicNodes`/`DynamicRelationships`/search methods. Node-only and relationship-only operators (e.g. traversal) are gated by generic constraints on the operator itself (`where T : INode`), not by a separate receiver interface — `IGraphNodeQueryable<T>`/`IGraphRelationshipQueryable<T>` are `[Obsolete]` aliases kept for one release.

`IGraph` methods accept an optional `IGraphTransaction`. A null transaction means the provider creates a per-operation or per-query execution transaction and owns its lifecycle. A non-null transaction means the caller owns commit/rollback/disposal, and the provider must reject foreign transaction implementations with a clear graph exception.

The provider store owns database connectivity and releases provider resources. `IGraph` instances do not own provider resources and are not disposable. If a store accepts an externally-owned driver/client, the store must not dispose that external object; `Neo4jGraphStore(IDriver, ...)` follows that rule.

## Entities And Schema

Domain node types satisfy `INode`; relationship types satisfy `IRelationship`. Application models should normally inherit from the base `Node` and `Relationship` records, because direct interface implementation triggers analyzer warning GM011 unless the model needs full control. The base records generate opaque string IDs with `Guid.NewGuid().ToString("N")`. Providers must treat IDs as caller-visible opaque strings, not database-native IDs.

Labels and relationship types come from attributes first:

- `[Node("Person")]` controls the node label. A node type maps to exactly one label, unique (case-insensitive) across all node types loaded in the process; `SchemaRegistry` throws a `GraphException` on a collision.
- `[Relationship("KNOWS")]` controls the relationship type (also one per type, unique case-insensitively).
- Without an attribute, `Labels.GetLabelFromType` derives a label/type from the CLR type name.
- Dynamic nodes use `DynamicNode.Labels` (an arbitrary, caller-managed label set); dynamic relationships use `DynamicRelationship.Type`.

Runtime metadata properties are provider-populated. `INode.Labels` and `IRelationship.Type` are empty before persistence for base records and should be populated after create/read based on actual stored labels/types.

`SchemaRegistry` reflects node and relationship schemas from CLR types and attributes. Providers are responsible for initializing schema and indexes before operations that require them. The Neo4j provider initializes schema lazily before mutations and exposes `RecreateIndexesAsync()`.

## Marker-Method Protocol

Async terminal LINQ operators are represented in expression trees by internal marker methods in `src/Graph.Model/GraphQueryable/QueryTerminals.cs`. `QueryableAsyncExtensions` builds `MethodCallExpression` nodes for these marker methods, then calls `IGraphQueryProvider.ExecuteAsync`. `QueryTerminals` is `internal`, not public API — a provider needs `InternalsVisibleTo` from `Cvoya.Graph.Model` (already granted to `Cvoya.Graph.Model.Neo4j`) to reference its members directly.

Providers recognize marker methods (and the rest of the LINQ surface) by `MethodInfo` identity — comparing a call's generic method definition (or the method itself, for non-generic methods) against a table built once via reflection — not by matching `MethodInfo.Name` as a string. The Neo4j provider's table lives in `CypherQueryVisitor`'s `LinqOperatorDispatch` helper; a new provider should build an equivalent table rather than switching on method names, since name-based dispatch cannot distinguish overloads and can silently mis-dispatch if an unrelated method happens to share a name.

Marker overload families to support:

- Materializers: `ToListAsyncMarker`, `ToArrayAsyncMarker`, `ToDictionaryAsyncMarker`, `ToLookupAsyncMarker`.
- Element operators: `FirstAsyncMarker`, `FirstOrDefaultAsyncMarker`, `LastAsyncMarker`, `LastOrDefaultAsyncMarker`, `SingleAsyncMarker`, `SingleOrDefaultAsyncMarker`, `ElementAtAsyncMarker`, `ElementAtOrDefaultAsyncMarker`.
- Quantifiers/counts: `AnyAsyncMarker`, `AllAsyncMarker`, `ContainsAsyncMarker`, `CountAsyncMarker`, `LongCountAsyncMarker`.
- Aggregates: `SumAsyncMarker`, `AverageAsyncMarker`, `MinAsyncMarker`, `MaxAsyncMarker`.

The current Neo4j visitor dispatch table covers:

- Standard LINQ (both `System.Linq.Queryable`'s methods, reached when a chain degrades to plain `IQueryable<T>`, and `GraphQueryableExtensions`'s own graph-typed-chain-preserving equivalents): `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Take`, `Skip`, `Distinct`, `SelectMany`, `GroupBy`, `Join`, `Union`.
- Async marker terminals: `ToListAsyncMarker`, `ToArrayAsyncMarker`, `FirstAsyncMarker`, `FirstOrDefaultAsyncMarker`, `SingleAsyncMarker`, `SingleOrDefaultAsyncMarker`, `LastAsyncMarker`, `LastOrDefaultAsyncMarker`, `AnyAsyncMarker`, `AllAsyncMarker`, `CountAsyncMarker`, `LongCountAsyncMarker`, `SumAsyncMarker`, `AverageAsyncMarker`, `MinAsyncMarker`, `MaxAsyncMarker`, `ContainsAsyncMarker`, `ElementAtAsyncMarker`, `ElementAtOrDefaultAsyncMarker`.
- Direct async names accepted by Neo4j (built by `QueryableAsyncExtensions.SumAsync`/`AverageAsync` alongside the marker path): `SumAsync`, `AverageAsync`.
- Graph extensions: `PathSegments`, `TraversePaths`, `Direction`, `WithDepth` (the last two are `[Obsolete]` free-floating modifiers, still dispatched for backward compatibility — new code should use the depth/direction overloads on `Traverse`/`TraversePaths` or an options lambda), `Search`.
- Synchronous fallbacks currently accepted for a subset: `First`, `FirstOrDefault`.

Note: `ReverseTraverse` is intentionally *not* in the dispatch table. It is a client-side extension method that eagerly composes `PathSegments().Direction(Incoming).Select(ps => ps.EndNode)` and calls `source.Provider.CreateQuery<T>` immediately rather than deferring, so the literal method call never reaches the visitor as a `MethodCallExpression` node — registering a handler for it would be dead code (this was a finding from the #80 characterization work).

**Two-arg traversal surface (issue #94, "Option C").** `Traverse<TRel, TEnd>`, `TraverseRelationships<TRel, TEnd>`, and `TraversePaths<TRel, TEnd>` are declared `this IGraphQueryable<INode> source` — the start node type is not one of the method's own generic arguments (`IGraphQueryable<T>` is covariant, so any `IGraphQueryable<TStart>` where `TStart : INode` converts to `IGraphQueryable<INode>` at the call site). A provider must therefore recover the start type from the **source expression's static element type** (`TypeHelpers.GetElementType(node.Arguments[0].Type)` in the Neo4j visitor), not from the call's generic arguments — by the time a `TraversePaths` `MethodCallExpression` is visited, its own generic arguments are only `(TRel, TEnd)`. `PathSegments<TStart, TRel, TEnd>` is unaffected: it keeps all three type arguments (its result type names the start type), so its dispatch handler still reads `TStart` directly off the call's generic arguments. The three-arg forms of `Traverse`/`TraverseRelationships`/`TraversePaths`/`ReverseTraverse` are `[Obsolete]` shims that delegate to the two-arg implementation client-side before any expression reaches the provider — a provider only ever needs to translate the two-arg shape (or, for `PathSegments`-derived compositions like `Traverse`, the `PathSegments` call the two-arg operator builds internally).

**Operators chained after `TraversePaths` — reject, don't mistranslate.** `TraversePaths` produces a per-hop RETURN shape (one row per hop of a captured variable-length path), not one row per `IGraphPath` — so almost no downstream LINQ operator has a well-defined row/alias to translate against. The Neo4j visitor enforces this with a single choke point (`CypherQueryVisitor.ThrowIfUnsupportedAfterTraversePaths`, called from `HandleLinqMethod` immediately after the source is visited, before dispatching to any handler): every operator is rejected with a `NotSupportedException` naming the operator, **except** a small whitelist that's actually safe (`ToListOrArray`/materializing terminals, and `Direction`/`WithDepth` when they're the wrapper the `TraversePaths(configure)` options-lambda overload itself builds). A new provider should implement the equivalent whitelist-at-a-single-choke-point shape rather than adding a per-handler check to every operator — the latter is easy to miss for a newly-added operator and reintroduces exactly the silent-mistranslation risk this guard exists to prevent.

Not every marker family is fully dispatched by Neo4j today. A new provider should match current behavior for compatibility and clearly fail unsupported methods with `GraphException` rather than falling back to client-side execution unexpectedly.

## Storage Conventions

Cross-provider compatibility depends on matching these conventions exactly.

### IDs

Store the public `IEntity.Id` as a normal graph property named `Id`. Do not expose database-native IDs as GraphModel IDs. Relationship `StartNodeId` and `EndNodeId` refer to GraphModel node IDs.

### Labels And Types

For typed nodes, store labels derived from `[Node]` or the CLR type fallback. For dynamic nodes, store `DynamicNode.Labels` exactly. Neo4j also stores the runtime `Labels` property on the node to support materialization.

For typed relationships, store the relationship type from `[Relationship]` or the CLR type fallback. For dynamic relationships, store `DynamicRelationship.Type` exactly.

When dynamic or serialized complex-property types need database-safe labels, the current runtime serializer uses a Neo4j-safe CLR type encoding (`SanitizeTypeNameForNeo4j` in `EntityFactory`). Treat that as a de facto current convention only; naming cleanup is expected as the provider abstraction matures.

### Simple Properties

Simple properties are properties whose types pass `GraphDataModel.IsSimple` or `IsCollectionOfSimple`: primitives, enums, string, `Point`, temporal types, `decimal`, `Guid`, byte arrays, `Uri`, and collections of simple values. Providers should serialize these directly to backend-native property values where possible.

`[Property]` controls storage names, key/index/full-text inclusion, and required/ignored semantics through `SchemaRegistry`. Relationship entities may only have simple properties; the Neo4j provider rejects complex properties on relationships.

### Complex Properties

Complex node properties are stored as provider-private graph nodes connected from the owner by relationship types in this exact form:

```text
__PROPERTY__{propertyName}__
```

The constants live in `GraphDataModel.PropertyRelationshipTypeNamePrefix` and `GraphDataModel.PropertyRelationshipTypeNameSuffix`; use `GraphDataModel.PropertyNameToRelationshipTypeName`.

Collections of complex properties use one relationship per collection item and store a `SequenceNumber` relationship property. Deserialization orders collection items by `SequenceNumber`. Nested complex properties recurse with the same relationship-type convention.

Providers must exclude these private `__PROPERTY__...__` relationships from user-visible relationship queries and normal cascade/delete semantics except where complex-property cleanup is explicitly required.

### Type Metadata

The de facto metadata convention is a property named `__metadata__` containing a `type` key with the assembly-qualified CLR type name when runtime type recovery is needed. Today this is defined in the Neo4j provider's `SerializationBridge`; providers should preserve the same shape until serialization is moved into a provider-neutral contract.

## Behavioral Contracts

Mutations must validate entity constraints before writing: non-null entities, non-empty IDs, relationship endpoints, required properties, and no reference cycles. Cycle detection is implemented by `GraphDataModel.HasReferenceCycle` / `EnsureNoReferenceCycle`.

Queries must stay provider-side for supported LINQ operators. Unsupported operators should fail with a clear `GraphException`. Avoid silent client-side evaluation unless the operator explicitly materializes results.

Transaction behavior:

- Null transaction: create a short-lived transaction/session for the operation or query execution.
- Caller transaction: execute inside the supplied provider transaction and do not commit or roll it back automatically.
- `CommitAsync()` completes the transaction; `RollbackAsync()` aborts it; disposal should clean up uncommitted transactions.

Full-text search is part of `IGraph`: `Search`, `SearchNodes`, `SearchRelationships`, and typed overloads — thin synchronous conveniences over the `.Search()` LINQ operator (building a queryable performs no I/O). Providers should respect `[Property(IncludeInFullTextSearch = false)]`, support dynamic entities, and initialize required full-text indexes.

Exception behavior follows the public API contract: provider/backend failures are wrapped in `GraphException`, missing entities from get/update/delete operations throw `EntityNotFoundException` (derived from `GraphException`), and invalid caller input preserves argument exceptions where the public API already does so.

## Contract-Test Reuse

The provider contract suite is the `Cvoya.Graph.Model.CompatibilityTests` package (`src/Graph.Model.CompatibilityTests`) - see [Certifying a provider](#certifying-a-provider) below for the full workflow. It mostly defines test interfaces with default xUnit test methods; running the package alone proves little because providers must bind those interfaces in a provider-specific test project.

The Neo4j provider pattern is:

- `tests/Graph.Model.Neo4j.Tests/Infrastructure/Neo4jHarness.cs` implements the suite's `IGraphProviderTestHarness` SPI, wrapping the existing Testcontainers/database-pool setup.
- `tests/Graph.Model.Neo4j.Tests/Neo4jTest.cs` derives from `CompatibilityTest`, adds correlation-scoped logging, and exposes `IGraph Graph`.
- Concrete classes in `tests/Graph.Model.Neo4j.Tests/GraphModelTests/` inherit `Neo4jTest` and implement one or more `Cvoya.Graph.Model.CompatibilityTests.I...Tests` interfaces.
- Provider-specific tests live beside the inherited contract tests.

A new provider test project follows the same three-piece shape (harness → intermediate base class → one-line interface bindings). `examples/CompatibilityTests.SampleHarness` is a compiling skeleton; `tests/Graph.Model.Neo4j.Tests` is the full reference implementation.

## Future Chapters

### Level-1 GraphQueryModel (#84)

`Cvoya.Graph.Model.Querying.GraphQueryModel` is the public, provider-neutral semantic query model. Its
roots, predicates, traversal steps, projection, ordering, paging, and terminal operation describe what a
query asks for without choosing a query language. Providers that do not target Cypher may consume this
model directly; the expression-to-model builder remains internal so the public LINQ surface and its
recognition table evolve as one release unit.

Recognition uses `MethodInfo` identity against the shared dispatch table, never method-name strings. Before
recognition, the front-end enforces configurable expression-tree bounds (10,000 nodes and depth 100 by
default). Funcletization evaluates parameter-free closure values and method calls; expressions referencing a
query parameter are not evaluated during translation. This is a structural allowlist, not a security sandbox
for application code.

### Dialect Capabilities (#85)

Stub. #85 will define dialect feature switches and the neutral result wire model. Until it lands, providers should document unsupported LINQ/search features and fail clearly.

## Certifying A Provider

The `Cvoya.Graph.Model.CompatibilityTests` package is a shippable TCK: a harness SPI, a capability registry so backends that legitimately lack a feature (e.g. server-side full-text search) skip rather than fail, and a compliance guard that catches a mis-wired provider project (one that discovers/runs far fewer tests than it should) instead of letting it silently "pass" with almost nothing executed.

### 1. Implement the harness SPI

```csharp
public sealed class MyProviderHarness : IGraphProviderTestHarness
{
    public string ProviderName => "MyCompany.GraphModel.MyProvider";

    // Declare only what your backing store actually supports. Unlisted capabilities' tests skip,
    // never fail - see GraphCapability in src/Graph.Model for the full member list.
    public CapabilitySet Capabilities => CapabilitySet.All.Except(GraphCapability.FullTextSearch);

    public ValueTask InitializeAsync() => /* start/connect infrastructure once per test class */;
    public ValueTask DisposeAsync() => /* release it */;

    public ValueTask<IGraph> GetGraphAsync(StoreIsolation isolation, CancellationToken ct)
    {
        // StoreIsolation.CleanSharedStore: reuse the per-class store, wipe its data.
        // StoreIsolation.FreshStore: provision a brand-new store (needed where a data wipe alone
        // doesn't reset auxiliary state, e.g. full-text index state).
        // Throw GraphProviderUnavailableException if infrastructure (e.g. Docker) can't start -
        // it renders as a skip locally, and a failure under GRAPHMODEL_COMPLIANCE_STRICT=1.
    }
}
```

### 2. Extend `CompatibilityTest` once

```csharp
public abstract class MyProviderTest(MyProviderHarness harness)
    : CompatibilityTest(harness), IClassFixture<MyProviderHarness>;
```

### 3. Bind the `I*Tests` interfaces

One line per suite interface (see `src/Graph.Model.CompatibilityTests/I*.cs` for the full set):

```csharp
public class BasicTests(MyProviderHarness h) : MyProviderTest(h), IBasicTests;
public class FullTextSearchTests(MyProviderHarness h) : MyProviderTest(h, StoreIsolation.FreshStore), IFullTextSearchTests;
// ... one per interface
```

### 4. Arm the compliance guard

```csharp
[assembly: AssemblyFixture(typeof(Cvoya.Graph.Model.CompatibilityTests.ComplianceGuard))]
```

The guard is unarmed by default (a local run with no reachable backing store stays a plain skip). Set `GRAPHMODEL_COMPLIANCE_STRICT=1` to arm it - CI compliance lanes should always run this way:

```bash
GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test <your-test-project> --report-trx
```

Under strict mode, the guard also promotes `GraphProviderUnavailableException` (unavailable infrastructure) from a skip to a hard failure, so a compliance lane can never "pass" simply because its backing store never came up.

### 5. Read the results

- **Capability skips** carry a fixed, parseable reason: `Capability '<Name>' not declared by provider '<ProviderName>' (Cvoya.Graph.Model.CompatibilityTests <version>)`. Any other skip or a nonzero failure count needs investigation.
- **The compliance report**: fill in `COMPLIANCE.md` (template in `src/Graph.Model.CompatibilityTests/COMPLIANCE.md`) from your TRX results - N passed / M skipped-by-declared-capability / 0 failed, where N is at least `ComplianceInventory.MinimumExecuted(yourDeclaredCapabilities)`.
- **"Compatible"** means: 0 failed, every skip is a declared-capability skip, and the executed count meets the guard's floor for your declared capabilities.

See `examples/CompatibilityTests.SampleHarness` for a minimal compiling skeleton of all three pieces, and `tests/Graph.Model.Neo4j.Tests` for the full in-tree reference implementation.

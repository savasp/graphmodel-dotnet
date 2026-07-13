---
---

# Provider Implementers Guide

This guide documents the current provider contract. It describes what a provider must implement today and the storage/query conventions that must remain compatible across providers.

The shared `GraphQueryModel`, Cypher dialect SPI, neutral result wire model, and reusable provider certification suite have landed. See [Implementing a Cypher provider](#implementing-a-cypher-provider) and [Certifying a provider](#certifying-a-provider).

## Public SPI

A provider exposes a store type that owns database connectivity and returns an `IGraph` instance. The in-tree provider uses `Neo4jGraphStore` as the public type and keeps `Neo4jGraph` internal. New providers should follow the same shape: one public `XGraphStore` type, optional provider-specific extension methods, and an internal `IGraph` implementation.

The public interfaces to implement are:

- `IGraph` in `src/Graph/IGraph.cs`: CRUD, synchronous query roots, full-text search, schema/index provisioning, and transactions.
- `IGraphTransaction` in `src/Graph/IGraphTransaction.cs`: `CommitAsync()`, `RollbackAsync()`, and `IAsyncDisposable`.
- `IGraphQueryProvider` in `src/Graph/GraphQueryable/IGraphQueryProvider.cs`: expression execution, query creation, and async terminal execution.
- `IGraphQueryable<T>` in `src/Graph/GraphQueryable/`: the single LINQ root returned by `IGraph.Nodes<TNode>`/`Relationships<TRelationship>`/`DynamicNodes`/`DynamicRelationships`/search methods. Node-only and relationship-only operators (e.g. traversal) are gated by generic constraints on the operator itself (`where T : INode`), not by a separate receiver interface — `IGraphNodeQueryable<T>`/`IGraphRelationshipQueryable<T>` are `[Obsolete]` aliases kept for one release.

`IGraph` methods accept an optional `IGraphTransaction`. A null transaction means the provider creates a per-operation or per-query execution transaction and owns its lifecycle. A non-null transaction means the caller owns commit/rollback/disposal, and the provider must reject foreign transaction implementations with a clear graph exception.

The provider store owns database connectivity and releases provider resources. `IGraph` instances do not own provider resources and are not disposable. If a store accepts an externally-owned driver/client, the store must not dispose that external object; `Neo4jGraphStore(IDriver, ...)` follows that rule.

## Entities And Schema

Domain node types satisfy `INode`; relationship types satisfy `IRelationship`. Application models should normally inherit from the base `Node` and `Relationship` records, because direct interface implementation triggers analyzer warning CG011 unless the model needs full control. The base records generate opaque string IDs with `Guid.NewGuid().ToString("N")`. Providers must treat IDs as caller-visible opaque strings, not database-native IDs.

Labels and relationship types come from attributes first:

- `[Node("Person")]` controls the node label. A node type maps to exactly one label, unique (case-insensitive) across all node types loaded in the process; `SchemaRegistry` throws a `GraphException` on a collision.
- `[Relationship("KNOWS")]` controls the relationship type (also one per type, unique case-insensitively).
- Without an attribute, `Labels.GetLabelFromType` derives a label/type from the CLR type name.
- Dynamic nodes use `DynamicNode.Labels` (an arbitrary, caller-managed label set); dynamic relationships use `DynamicRelationship.Type`.

Runtime metadata properties are provider-populated. `INode.Labels` and `IRelationship.Type` are empty before persistence for base records and should be populated after create/read based on actual stored labels/types.

`SchemaRegistry` reflects node and relationship schemas from CLR types and attributes. Providers are responsible for initializing schema and indexes before operations that require them. The Neo4j provider initializes schema lazily before mutations and exposes `RecreateIndexesAsync()`.

## Marker-Method Protocol

Async terminal LINQ operators are represented in expression trees by internal marker methods in `src/Graph/GraphQueryable/QueryTerminals.cs`. `QueryableAsyncExtensions` builds `MethodCallExpression` nodes for these marker methods, then calls `IGraphQueryProvider.ExecuteAsync`. `QueryTerminals` is `internal`, not public API — a provider needs `InternalsVisibleTo` from `Cvoya.Graph` (already granted to `Cvoya.Graph.Neo4j`) to reference its members directly.

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

**Operators chained after `TraversePaths` — lower paths before hops.** Keep the captured variable-length path as one semantic row while applying path predicates/projections, pagination, and cardinality terminals. The shared model supports `Where`, `Select`, `Take`, `Skip`, `Count`, and `Any`; only a path-valued materialization is decomposed into the per-hop wire shape consumed by `GraphResultProcessor.GraphPathHop`. The builder retains a single whitelist choke point for everything else: an unsupported operator must throw a named exception rather than translate against a hop alias. It also enforces composition order — the model applies path predicates before projection and pagination, so a `Where` that textually follows `Select`/`Take`/`Skip` on a path query is rejected at build time instead of silently filtering the wrong row set (an identity `Select(p => p)` is dropped as a projection no-op). Providers should consume this shared model boundary and preserve the same translates-correctly-or-throws-clearly dichotomy.

Not every marker family is fully dispatched by Neo4j today. A new provider should match current behavior for compatibility and clearly fail unsupported methods with `GraphException` rather than falling back to client-side execution unexpectedly.

## Storage Conventions

Cross-provider compatibility depends on matching these conventions exactly.

### IDs

Store the public `IEntity.Id` as a normal graph property named `Id`. Do not expose database-native IDs as CVOYA graph IDs. Relationship `StartNodeId` and `EndNodeId` refer to CVOYA graph node IDs.

### Labels And Types

For typed nodes, store labels derived from `[Node]` or the CLR type fallback. For dynamic nodes, store `DynamicNode.Labels` exactly. Neo4j also stores the runtime `Labels` property on the node to support materialization.

For typed relationships, store the relationship type from `[Relationship]` or the CLR type fallback. For dynamic relationships, store `DynamicRelationship.Type` exactly.

When dynamic or serialized complex-property types need database-safe labels, the current runtime serializer uses a Neo4j-safe CLR type encoding (`SanitizeTypeNameForNeo4j` in `EntityFactory`). Treat that as a de facto current convention only; naming cleanup is expected as the provider abstraction matures.

### Simple Properties

Simple properties are properties whose types pass `GraphDataModel.IsSimple` or `IsCollectionOfSimple`: primitives, enums, string, `Point`, temporal types, `decimal`, `Guid`, byte arrays, `Uri`, and collections of simple values. Providers should serialize these directly to backend-native property values where possible.

`[Property]` controls storage names, key/index/full-text inclusion, and required/ignored semantics through `SchemaRegistry`. Relationship entities may only have simple properties; the Neo4j provider rejects complex properties on relationships.

### Complex Properties

Complex properties are first-class graph structure. A property named `Home` whose CLR type is
`Address` is stored as:

```text
(:Person)-[:Home]->(:Address)
```

The relationship type is the property name by convention. `[ComplexProperty(RelationshipType =
"LIVES_AT")]` overrides it. Providers should use
`GraphDataModel.GetComplexPropertyRelationshipType` so generated and reflection-based serializers
agree on the mapping.

Collections of complex properties use one relationship per collection item and store a `SequenceNumber` relationship property. Deserialization orders collection items by `SequenceNumber`. Nested complex properties recurse with the same relationship-type convention.

Every occurrence is a separate value node, even when two owners reference the same in-memory object.
Nodes and relationships need stable CVOYA graph IDs and remain visible to ordinary graph queries.
Providers may add an internal relationship marker for cascade cleanup, but must not infer
complex-property edges from a reserved relationship-name prefix.

Declared properties auto-load recursively. Writes and reads must reject cycles and paths deeper than
`GraphDataModel.DefaultDepthAllowed` (currently 5). A read type that omits the property leaves it
unpopulated; callers can still traverse the semantic relationship and co-load the owner and value via
the normal path-segment projection.

### Type Metadata

The metadata convention is a property named `__metadata__` containing a `type` key with the assembly-qualified CLR type name when runtime type recovery is needed. `GraphResultProcessor` reads this shape from neutral wire values before falling back to node labels or relationship type for polymorphic resolution. Providers must preserve the shape on writes and adapt the metadata map like any other result map.

## Behavioral Contracts

Mutations must validate entity constraints before writing: non-null entities, non-empty IDs, relationship endpoints, required properties, no reference cycles, and bounded complex-property depth. Cycle detection is implemented by `GraphDataModel.HasReferenceCycle` / `EnsureNoReferenceCycle`; depth validation uses `EnsureComplexPropertyDepth`.

Queries must stay provider-side for supported LINQ operators. Unsupported operators should fail with a clear `GraphException`. Avoid silent client-side evaluation unless the operator explicitly materializes results.

Transaction behavior:

- Null transaction: create a short-lived transaction/session for the operation or query execution.
- Caller transaction: execute inside the supplied provider transaction and do not commit or roll it back automatically.
- `CommitAsync()` completes the transaction; `RollbackAsync()` aborts it; disposal should clean up uncommitted transactions.

Full-text search is part of `IGraph`: `Search`, `SearchNodes`, `SearchRelationships`, and typed overloads — thin synchronous conveniences over the `.Search()` LINQ operator (building a queryable performs no I/O). Providers should respect `[Property(IncludeInFullTextSearch = false)]`, support dynamic entities, and initialize required full-text indexes.

Exception behavior follows the public API contract: provider/backend failures are wrapped in `GraphException`, missing entities from get/update/delete operations throw `EntityNotFoundException` (derived from `GraphException`), and invalid caller input preserves argument exceptions where the public API already does so.

## Contract-Test Reuse

The provider contract suite is the `Cvoya.Graph.CompatibilityTests` package (`src/Cvoya.Graph.CompatibilityTests`) - see [Certifying a provider](#certifying-a-provider) below for the full workflow. It mostly defines test interfaces with default xUnit test methods; running the package alone proves little because providers must bind those interfaces in a provider-specific test project.

The in-memory provider is the worked harness example:

- `tests/Graph.InMemory.Tests/Infrastructure/InMemoryHarness.cs` implements `IGraphProviderTestHarness` with no external infrastructure.
- `tests/Graph.InMemory.Tests/InMemoryTest.cs` derives from `CompatibilityTest` and exposes `IGraph Graph`.
- Concrete classes in `tests/Graph.InMemory.Tests/GraphTests/` inherit `InMemoryTest` and implement one or more `Cvoya.Graph.CompatibilityTests.I...Tests` interfaces.
- Provider-specific tests live beside the inherited contract tests.

A new provider test project follows the same three-piece shape (harness → intermediate base class → one-line interface bindings). `examples/CompatibilityTests.SampleHarness` is a compiling skeleton; `tests/Graph.InMemory.Tests` is the in-tree worked implementation, and `tests/Graph.Age.Tests` demonstrates the same SPI with an external database.

## Shared Query Translation

### Level-1 GraphQueryModel

`Cvoya.Graph.Querying.GraphQueryModel` is the public, provider-neutral semantic query model. Its
roots, predicates, traversal steps, projection, ordering, paging, and terminal operation describe what a
query asks for without choosing a query language. Providers that do not target Cypher may consume this
model directly; the expression-to-model builder remains internal so the public LINQ surface and its
recognition table evolve as one release unit.

Recognition uses `MethodInfo` identity against the shared dispatch table, never method-name strings. Before
recognition, the front-end enforces configurable expression-tree bounds (10,000 nodes and depth 100 by
default). Funcletization evaluates parameter-free closure values and method calls; expressions referencing a
query parameter are not evaluated during translation. This is a structural allowlist, not a security sandbox
for application code.

## Implementing A Cypher Provider

A minimal Cypher provider supplies three provider-specific pieces: an `ICypherDialect`, an executor, and a driver-to-wire adapter. It reuses `GraphQueryModel`, `CypherQueryPlanner`, `CypherRenderer`, `GraphResultMaterializer`, and the compatibility suite. Do not fork the planner, renderer, complex-property reassembly, polymorphic type resolution, or path stitching.

### 1. Declare the dialect

Implement `ICypherDialect` from `Cvoya.Graph.Cypher`. The interface owns every syntax choice that can vary by backend:

- parameter references and property/ID access;
- identifier escaping, node-label lists, relationship-type lists, and depth ranges;
- provider-neutral function names such as `temporal.datetime` and `string.join`;
- label predicates and the complex-property relationship marker;
- full-text procedure and index names; and
- a `CapabilitySet` using the same `GraphCapability` enum as the compatibility suite.

`GetFunctionBehavior` distinguishes functions rendered by the backend, parameter-free functions evaluated on the client, and unsupported functions. Client evaluation binds a query parameter; it never inlines the value. A function marked `EvaluateOnClient` fails translation if its arguments depend on a server-side expression. AGE can therefore client-evaluate zero-argument temporal constructors without pretending to support temporal arithmetic over stored properties.

Declare only supported capabilities. The planner rejects reachable unsupported constructs before execution with `GraphQueryTranslationException`; the message names the construct, the exact `GraphCapability` member, and the dialect. Current translation-time checks cover `FullTextSearch`, `CallSubqueries`, `PatternSizeProjection`, `MultiLabelMatch`, `OrderByEntity`, and `OptionalTraversal`. Transaction capabilities remain execution/store concerns.

For example, an initial AGE dialect should decline at least `FullTextSearch`, `NestedTransactions`, `CallSubqueries`, `PatternSizeProjection`, `MultiLabelMatch`, and `OrderByEntity` unless its renderer strategy genuinely supports them. Do not emulate unsupported full-text search with a semantically weaker regular-expression query while still declaring `FullTextSearch`.

Declaring `FullTextSearch` guarantees: case-insensitive, exact-token (whole-word) matching; a multi-term query matches an entity iff ALL terms match, in any order and at any distance; the matched property set is exactly the entity's own `[Property(IncludeInFullTextSearch)]` string properties (string-only by construction; for dynamic entities, all string property values). Text on complex-property value nodes is NOT part of the owning entity's match set. Ranking, stemming, phrase adjacency, prefix/wildcard, and matching beyond the floor are provider-defined: the TCK asserts nothing about them and never asserts a non-match for near-tokens (only for sub-tokens, which must not match). Search result order is unspecified; ordering comes only from explicit `OrderBy`. Providers share one definition of a "term" via `FullTextQueryTokenizer` (split on any non-letter/non-digit, lowercase invariant, drop empties) and each lower it into their own engine syntax; the shared planner keeps the raw query string.

Provider-specific dynamic-entity accessor methods must carry `[CypherDynamicEntityAccessor]`. The planner checks the attribute on the exact `MethodInfo`; a matching method or declaring-type name is not sufficient.

### 2. Plan and render

Build the shared semantic model, then pass the same dialect instance to planning and rendering:

```csharp
var model = GraphQueryModelBuilder.Build(expression);
var statement = new CypherQueryPlanner(dialect).Plan(model);
CypherRenderResult rendered = new CypherRenderer(dialect).Render(statement);
```

`CypherRenderResult` carries query text, the parameter map, and the exact ordered projection-column names. The column schema is part of the executor contract, not optional metadata: Apache AGE needs it to build the mandatory `AS ("alias" agtype, ...)` SQL column-definition list without reparsing `RETURN`. Preserve alias spelling and casing. Parameter transport remains executor-specific; for example, AGE may serialize the map into one `agtype` parameter even though in-query references use `$name`.

### 3. Adapt driver values

The executor sends `CypherRenderResult.Text` and parameters to the backend and adapts every returned row into `GraphRecord`. Build values only through the validated `GraphValue` factories:

- `Scalar` for provider-neutral CLR scalars (`long`, `decimal`, `DateOnly`, `DateTimeOffset`, `Point`, and so on);
- `Node` with an opaque provider element ID, adapter-populated labels, and recursively adapted properties;
- `Relationship` with opaque element/end-point IDs, type, and properties;
- `Path` for an alternating node/relationship sequence;
- `List` and `Map` for recursive collection/projection values.

Factories defensively copy collections and reject invalid path shapes or null entries (represent null as `GraphValue.Scalar(null)`). Adapters must not leak driver types. Preserve integer versus floating-point values and decimal precision; stringify neither IDs nor numerics merely to simplify conversion. For AGE, reconstruct inheritance labels from its stored `inheritance_labels` representation before calling `GraphValue.Node`.

Pass buffered records to `GraphResultMaterializer.MaterializeAsync<T>` or individual records to `MaterializeRecordAsync<T>`. The shared materializer owns complex-property subtree assembly, label/`__metadata__` polymorphism, relationship endpoint reconstruction, scalar conversion, projections, and path stitching. An adapter translates values only; it must not choose CLR domain types or rebuild owned-property graphs.

### Provider-facing shared types

The implementation seam is intentionally small:

- `ICypherDialect`;
- `CypherRenderResult`;
- `GraphRecord`;
- `GraphValue`; and
- `GraphResultProcessor.GraphPathHop` only when streaming decomposed `IGraphPath` rows.

`CypherRenderer`, `GraphResultMaterializer`, and `GraphResultProcessor` are shared services, not provider implementations. The in-tree `Neo4jRecordAdapter` is the reference boundary: it is driver-only and contains no materialization policy.

## Certifying A Provider

The `Cvoya.Graph.CompatibilityTests` package is a shippable TCK: a harness SPI, a capability registry so backends that legitimately lack a feature (e.g. server-side full-text search) skip rather than fail, and a compliance guard that catches a mis-wired provider project (one that discovers/runs far fewer tests than it should) instead of letting it silently "pass" with almost nothing executed.

### 1. Implement the harness SPI

```csharp
public sealed class MyProviderHarness : IGraphProviderTestHarness
{
    public string ProviderName => "MyCompany.CVOYA graph.MyProvider";

    // Declare only what your backing store actually supports. Unlisted capabilities' tests skip,
    // never fail - see GraphCapability in src/Graph for the full member list.
    public CapabilitySet Capabilities => CapabilitySet.Of(
        GraphCapability.Transactions,
        GraphCapability.ComplexPropertyCascade);

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

    public ValueTask<int> CountNodesByPropertyAsync(
        IGraph graph,
        string label,
        string propertyName,
        IReadOnlyCollection<string> values,
        CancellationToken ct)
    {
        // Execute the narrowest provider-native count needed by the complex-property orphan
        // contracts. This is not a general raw-query escape hatch.
    }

    public bool IsExpectedConcurrentUpdateException(Exception exception)
    {
        // Return true only for provider-specific serialization, deadlock, or retryable write
        // conflicts that may legitimately reject one of two same-node updates.
    }
}
```

### 2. Extend `CompatibilityTest` once

```csharp
public abstract class MyProviderTest(MyProviderHarness harness)
    : CompatibilityTest(harness), IClassFixture<MyProviderHarness>;
```

### 3. Bind the `I*Tests` interfaces

One line per suite interface (see `src/Cvoya.Graph.CompatibilityTests/I*.cs` for the full set):

```csharp
public class BasicTests(MyProviderHarness h) : MyProviderTest(h), IBasicTests;
public class FullTextSearchTests(MyProviderHarness h) : MyProviderTest(h, StoreIsolation.FreshStore), IFullTextSearchTests;
// ... one per interface
```

### 4. Arm the compliance guard

```csharp
[assembly: AssemblyFixture(typeof(Cvoya.Graph.CompatibilityTests.ComplianceGuard))]
```

The guard is unarmed by default (a local run with no reachable backing store stays a plain skip). Set `GRAPHMODEL_COMPLIANCE_STRICT=1` to arm it - CI compliance lanes should always run this way:

```bash
GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test <your-test-project> --report-trx
```

Under strict mode, the guard also promotes `GraphProviderUnavailableException` (unavailable infrastructure) from a skip to a hard failure, so a compliance lane can never "pass" simply because its backing store never came up.

### 5. Read the results

- **Capability skips** carry a fixed, parseable reason: `Capability '<Name>' not declared by provider '<ProviderName>' (Cvoya.Graph.CompatibilityTests <version>)`. Any other skip or a nonzero failure count needs investigation.
- **The compliance report**: fill in `COMPLIANCE.md` (template in `src/Cvoya.Graph.CompatibilityTests/COMPLIANCE.md`) from your TRX results - N passed / M skipped-by-declared-capability / 0 failed, where N is at least `ComplianceInventory.MinimumExecuted(yourDeclaredCapabilities)`.
- **"Compatible"** means: 0 failed, every skip is a declared-capability skip, and the executed count meets the guard's floor for your declared capabilities.

See `examples/CompatibilityTests.SampleHarness` for a minimal compiling skeleton of all three pieces, and `tests/Graph.InMemory.Tests` for the full in-tree worked implementation.

### 6. Capability coverage map

Every optional `GraphCapability` is either certified by a `[RequiresCapability]`-gated test (which runs where the harness declares the capability and skips-with-reason where it doesn't) or recorded here as having no certifiable user surface yet. The last three columns show what the in-tree harnesses do today. Neo4j declares every capability with a user-visible surface; in-memory and AGE also declare features they provide through equivalent evaluation or lowering rather than native Cypher syntax.

| Capability | Certifying test(s) | Attribution | Neo4j | in-memory | AGE |
|---|---|---|---|---|---|
| `FullTextSearch` | `IFullTextSearchTests` (all methods) | interface | pass | skip | skip |
| `Transactions` | `ITransactionTests` (all methods) | interface | pass | pass | pass |
| `ComplexPropertyCascade` | `IComplexObjectGraphSerializationTests` (all methods) | interface | pass | pass | pass |
| `CallSubqueries` | `IAdvancedQueryTests` correlated-collection pattern comprehensions (`CanQueryWith{Basic,Filtered,Aggregated,TimeBased,Ordered,Grouped}PatternComprehension`, `CanQueryWithTraversePathAndGroupBy`) | method | pass | pass | skip |
| `PatternSizeProjection` | `IAdvancedQueryTests.CanProjectComplexCollectionSize` | method | pass | pass | skip |
| `MultiLabelMatch` | `IAdvancedQueryTests.CanQueryPolymorphicBaseTypeAcrossSubtypeLabels` | method | pass | pass | pass |
| `OrderByEntity` | `IAdvancedQueryTests.CanOrderByBareEntity` | method | pass | skip | pass |
| `OptionalTraversal` | `IQueryTests.Navigation{Equality,Projection}_MissingComplexProperty*` | method | pass | pass | pass |
| `NestedTransactions` | _record only_ | — | — | — | — |
| `ShortestPath` | _record only_ | — | — | — | — |

Two capabilities are records rather than gated tests because they have no user-drivable surface to certify:

- **`NestedTransactions`** — the public transaction API takes no parent transaction or savepoint, so there is no nested operation to drive. No in-tree provider declares it. When the API gains that surface, add a gated test with the feature.
- **`ShortestPath`** — reserved; no query construct references it and no in-tree provider declares it, so there is nothing to exercise yet.

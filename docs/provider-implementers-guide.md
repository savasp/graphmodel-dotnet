# Provider Implementers Guide

This guide documents the current provider contract. It describes what a provider must implement today and the storage/query conventions that must remain compatible across providers.

Future provider work is tracked separately: the shared `GraphQueryModel` layer is #84, dialect capabilities are #85, MethodInfo-based terminal dispatch is #94, and reusable provider certification is #95.

## Public SPI

A provider exposes a store type that owns database connectivity and returns an `IGraph` instance. The in-tree provider uses `Neo4jGraphStore` as the public type and keeps `Neo4jGraph` internal. New providers should follow the same shape: one public `XGraphStore` type, optional provider-specific extension methods, and an internal `IGraph` implementation.

The public interfaces to implement are:

- `IGraph` in `src/Graph.Model/IGraph.cs`: CRUD, query roots, full-text search, schema/index provisioning, transactions, and disposal.
- `IGraphTransaction` in `src/Graph.Model/IGraphTransaction.cs`: `CommitAsync()`, `Rollback()`, and `IAsyncDisposable`.
- `IGraphQueryProvider` in `src/Graph.Model/GraphQueryable/IGraphQueryProvider.cs`: expression execution, query creation, and async terminal execution.
- `IGraphQueryable<T>`, `IGraphNodeQueryable<TNode>`, and `IGraphRelationshipQueryable<TRel>` in `src/Graph.Model/GraphQueryable/`: LINQ roots returned by `IGraph`.

`IGraph` methods accept an optional `IGraphTransaction`. A null transaction means the provider creates a per-operation or per-query execution transaction and owns its lifecycle. A non-null transaction means the caller owns commit/rollback/disposal, and the provider must reject foreign transaction implementations with a clear graph exception.

`IGraph.DisposeAsync()` releases provider resources owned by the graph. If a store accepts an externally-owned driver/client, the store should not dispose that external object; `Neo4jGraphStore(IDriver, ...)` follows that rule.

## Entities And Schema

Domain nodes implement `INode`; relationships implement `IRelationship`. The base `Node` and `Relationship` records generate opaque string IDs with `Guid.NewGuid().ToString("N")`. Providers must treat IDs as caller-visible opaque strings, not database-native IDs.

Labels and relationship types come from attributes first:

- `[Node("Person")]` or `[Node("Person", "Employee")]` controls node labels.
- `[Relationship("KNOWS")]` controls relationship type.
- Without an attribute, `Labels.GetLabelFromType` derives a label/type from the CLR type name.
- Dynamic nodes use `DynamicNode.Labels`; dynamic relationships use `DynamicRelationship.Type`.

Runtime metadata properties are provider-populated. `INode.Labels` and `IRelationship.Type` are empty before persistence for base records and should be populated after create/read based on actual stored labels/types.

`SchemaRegistry` reflects node and relationship schemas from CLR types and attributes. Providers are responsible for initializing schema and indexes before operations that require them. The Neo4j provider initializes schema lazily before mutations and exposes `RecreateIndexesAsync()`.

## Marker-Method Protocol

Async terminal LINQ operators are represented in expression trees by internal marker methods in `src/Graph.Model/GraphQueryable/QueryableAsyncExtensionsMarkers.cs`. `QueryableAsyncExtensions` builds `MethodCallExpression` nodes for these marker methods, then calls `IGraphQueryProvider.ExecuteAsync`.

Current providers must recognize marker methods by string `MethodInfo.Name`. The Neo4j provider dispatches names in `CypherQueryVisitor.HandleLinqMethod`. This is intentionally documented as current behavior, not a stable design: #94 replaces the string-name switch with MethodInfo-table dispatch.

Marker overload families to support:

- Materializers: `ToListAsyncMarker`, `ToArrayAsyncMarker`, `ToDictionaryAsyncMarker`, `ToLookupAsyncMarker`.
- Element operators: `FirstAsyncMarker`, `FirstOrDefaultAsyncMarker`, `LastAsyncMarker`, `LastOrDefaultAsyncMarker`, `SingleAsyncMarker`, `SingleOrDefaultAsyncMarker`, `ElementAtAsyncMarker`, `ElementAtOrDefaultAsyncMarker`.
- Quantifiers/counts: `AnyAsyncMarker`, `AllAsyncMarker`, `ContainsAsyncMarker`, `CountAsyncMarker`, `LongCountAsyncMarker`.
- Aggregates: `SumAsyncMarker`, `AverageAsyncMarker`, `MinAsyncMarker`, `MaxAsyncMarker`.

The current Neo4j visitor dispatch list is:

- Standard LINQ: `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Take`, `Skip`, `Distinct`, `SelectMany`, `GroupBy`, `Join`, `Union`.
- Async marker terminals: `ToListAsyncMarker`, `ToArrayAsyncMarker`, `FirstAsyncMarker`, `FirstOrDefaultAsyncMarker`, `SingleAsyncMarker`, `SingleOrDefaultAsyncMarker`, `LastAsyncMarker`, `LastOrDefaultAsyncMarker`, `AnyAsyncMarker`, `AllAsyncMarker`, `CountAsyncMarker`, `LongCountAsyncMarker`, `SumAsyncMarker`, `AverageAsyncMarker`, `MinAsyncMarker`, `MaxAsyncMarker`, `ContainsAsyncMarker`, `ElementAtAsyncMarker`, `ElementAtOrDefaultAsyncMarker`.
- Temporary direct async names accepted by Neo4j: `SumAsync`, `AverageAsync`.
- Graph extensions: `PathSegments`, `ReverseTraverse`, `Direction`, `WithDepth`, `Search`.
- Synchronous fallbacks currently accepted for a subset: `First`, `FirstOrDefault`.

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
- `CommitAsync()` completes the transaction; `Rollback()` aborts it; disposal should clean up uncommitted transactions.

Full-text search is part of `IGraph`: `SearchAsync`, `SearchNodesAsync`, `SearchRelationshipsAsync`, and typed overloads. Providers should respect `[Property(IncludeInFullTextSearch = false)]`, support dynamic entities, and initialize required full-text indexes.

Exception behavior is not fully cleaned up yet; #76 owns the final API contract. Until then, wrap provider/backend failures in `GraphException`, use `KeyNotFoundException` or `GraphException` consistently for missing entities based on the existing tests, and preserve argument exceptions for invalid caller input where the public API already does so.

## Contract-Test Reuse

`tests/Graph.Model.Tests` is the provider contract suite. It mostly defines test interfaces with default xUnit test methods; running that project alone proves little because providers must inherit those interfaces in a provider-specific test project.

The Neo4j provider pattern is:

- `tests/Graph.Model.Neo4j.Tests/Neo4jTest.cs` owns provider setup and exposes `IGraph Graph`.
- Concrete classes in `tests/Graph.Model.Neo4j.Tests/GraphModelTests/` inherit `Neo4jTest` and implement one or more `Cvoya.Graph.Model.Tests.I...Tests` interfaces.
- Provider-specific tests live beside the inherited contract tests.

A new provider test project should create an equivalent fixture/base class and inherit these contract interfaces:

- `IBasicTests`
- `IAdvancedQueryTests`
- `IAggregationTests`
- `IAttributeValidationTests`
- `IClassHierarchyTests`
- `IComplexObjectGraphSerializationTests`
- `IDynamicEntitySchemaValidationTests`
- `IErrorHandlingTests`
- `IFullTextSearchTests`
- `INullablePropertyDeserializationTests`
- `IQueryTests`
- `IQueryTraversalTests`
- `ISchemaDefinitionTests`
- `ITakeOperatorTests`
- `ITransactionTests`

Each provider may add backend-specific tests for connection setup, dialect behavior, and dynamic entity materialization, but the shared interfaces are the compatibility baseline.

## Future Chapters

### Level-1 GraphQueryModel (#84)

Stub. #84 will define the shared query IR and LINQ front-end extraction. Until it lands, providers implement their own expression visitor and use the current marker-method protocol.

### Dialect Capabilities (#85)

Stub. #85 will define dialect feature switches and the neutral result wire model. Until it lands, providers should document unsupported LINQ/search features and fail clearly.

### Certifying A Provider (#95)

Stub. #95 will package the contract tests as a reusable TCK with harness SPI and capability model. Until it lands, copy the Neo4j test inheritance pattern and run the shared test interfaces in the provider test project.

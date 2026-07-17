---
---

# Migration guide: Query surface v2 (issue #94)

CVOYA graph is pre-1.0 alpha; breaking changes are expected between releases. This guide covers
every breaking (and near-breaking) change from the "query surface v2" rework (issue #94). If
you're upgrading, work through the sections in order — later sections build on earlier ones.

## Final compatibility cleanup (issue #240)

The one-release compatibility window is closed. The following members are removed; migrate every
call site to the canonical replacement before upgrading:

| Removed member | Replacement |
|---|---|
| `IGraphNodeQueryable` | `IGraphQueryable` |
| `IGraphNodeQueryable<T>` | `IGraphQueryable<T>` |
| `IOrderedGraphNodeQueryable<T>` | `IOrderedGraphQueryable<T>` |
| `IGraphRelationshipQueryable` | `IGraphQueryable` |
| `IGraphRelationshipQueryable<T>` | `IGraphQueryable<T>` |
| `IOrderedGraphRelationshipQueryable<T>` | `IOrderedGraphQueryable<T>` |
| `WithDepth<T>(source, maxDepth)` | `Traverse<TRel, TEnd>(maxDepth)` or a traversal options lambda |
| `WithDepth<T>(source, minDepth, maxDepth)` | `Traverse<TRel, TEnd>(minDepth, maxDepth)`, `TraversePaths<TRel, TEnd>(minDepth, maxDepth)`, or a traversal options lambda |
| `Direction<T>(source, direction)` | `Traverse<TRel, TEnd>(direction)`, `PathSegments<TStart, TRel, TEnd>(direction)`, or a traversal options lambda |
| `Traverse<TStart, TRel, TEnd>()` | `Traverse<TRel, TEnd>()` |
| `Traverse<TStart, TRel, TEnd>(maxDepth)` | `Traverse<TRel, TEnd>(maxDepth)` |
| `Traverse<TStart, TRel, TEnd>(minDepth, maxDepth)` | `Traverse<TRel, TEnd>(minDepth, maxDepth)` |
| `Traverse<TStart, TRel, TEnd>(direction)` | `Traverse<TRel, TEnd>(direction)` |
| `Traverse<TStart, TRel, TEnd>(configure)` | `Traverse<TRel, TEnd>(configure)` |
| `ReverseTraverse<TStart, TRel, TEnd>()` | `ReverseTraverse<TRel, TEnd>()` |
| `TraverseRelationships<TStart, TRel, TEnd>()` | `TraverseRelationships<TRel, TEnd>()` |
| `TraversePaths<TStart, TRel, TEnd>(minDepth, maxDepth)` | `TraversePaths<TRel, TEnd>(minDepth, maxDepth)` |
| `TraversePaths<TStart, TRel, TEnd>(configure)` | `TraversePaths<TRel, TEnd>(configure)` |

## 1. Query roots are synchronous

`IGraph.NodesAsync<N>`, `RelationshipsAsync<R>`, `DynamicNodesAsync`, and `DynamicRelationshipsAsync`
are gone. Building a queryable never did I/O in the first place — the `Task<...>` wrapper only
added an `await` your code had to work around. Use the synchronous equivalents:

```diff
-var people = await (await graph.NodesAsync<Person>())
-    .Where(p => p.Age > 18)
-    .ToListAsync();
+var people = await graph.Nodes<Person>()
+    .Where(p => p.Age > 18)
+    .ToListAsync();
```

| Old | New |
|---|---|
| `Task<IGraphNodeQueryable<N>> NodesAsync<N>(tx = null)` | `IGraphQueryable<N> Nodes<N>(tx = null)` |
| `Task<IGraphRelationshipQueryable<R>> RelationshipsAsync<R>(tx = null)` | `IGraphQueryable<R> Relationships<R>(tx = null)` |
| `Task<IGraphNodeQueryable<DynamicNode>> DynamicNodesAsync(tx = null)` | `IGraphQueryable<DynamicNode> DynamicNodes(tx = null)` |
| `Task<IGraphRelationshipQueryable<DynamicRelationship>> DynamicRelationshipsAsync(tx = null)` | `IGraphQueryable<DynamicRelationship> DynamicRelationships(tx = null)` |

Whatever transaction/session acquisition the old `Task<...>` wrapper implied now happens at
*execution* time (`ToListAsync`, `FirstOrDefaultAsync`, `await foreach`, ...), which was already
async — nothing about transaction semantics changed, only when the (never-actually-async) root
construction stopped pretending to be async.

## 2. Single `IGraphQueryable<T>` — no more `IGraphNodeQueryable<T>`/`IGraphRelationshipQueryable<T>`

`IGraphNodeQueryable<T>` and `IGraphRelationshipQueryable<T>` (and their non-generic and ordered
counterparts) have been removed. Node-only and relationship-only operators (traversal,
`TraversePaths`, etc.) are now gated by generic constraints on the *operator*
(`where TStart : INode`), not by the receiver's static type:

```diff
-IGraphNodeQueryable<Person> people = await graph.NodesAsync<Person>();
+IGraphQueryable<Person> people = graph.Nodes<Person>();
```

If your code declared local variables or fields as `IGraphNodeQueryable<T>`/
`IGraphRelationshipQueryable<T>`, change the declared type to `IGraphQueryable<T>`. Method
signatures that accepted `IGraphNodeQueryable<T>` as a parameter should accept `IGraphQueryable<T>`
with a `where T : INode` constraint instead.

**Practical effect — this also fixes a real bug:** because there is only one queryable interface
now, `Where`/`OrderBy`/`Take`/`Distinct`/etc. all return `IGraphQueryable<T>` uniformly, so
`.OrderBy(...).Traverse<...>()` compiles — previously `OrderBy` returned
`IOrderedGraphQueryable<TSource>`, which was not an `IGraphNodeQueryable<TSource>`, so you could
not traverse after ordering without restructuring the query.

## 3. `IGraphQueryable<T>` implements `IAsyncEnumerable<T>`

You can now `await foreach` over a query directly:

```csharp
await foreach (var person in graph.Nodes<Person>().Where(p => p.Age > 18))
{
    Console.WriteLine(person.FirstName);
}
```

This buffers the full result set today (true incremental streaming is tracked separately, issue
#78) — it is not yet a substitute for paging large result sets, but it does remove the need for
`ToListAsync()` purely to get an enumerable.

**Overload resolution note:** .NET 10 ships `System.Linq.AsyncEnumerable`, a BCL extension-method
surface over `IAsyncEnumerable<T>` (`ToListAsync`, `FirstOrDefaultAsync`, etc.) that would
otherwise collide with CVOYA graph's own `QueryableAsyncExtensions` methods of the same name, now
that `IGraphQueryable<T>` implements both `IQueryable<T>` and `IAsyncEnumerable<T>`.
`QueryableAsyncExtensions`' primary overloads are typed `this IGraphQueryable<T>` (more specific
than either `IQueryable<T>` or `IAsyncEnumerable<T>`), so calls on an `IGraphQueryable<T>` resolve
unambiguously to CVOYA graph's own implementation. A small number of methods (currently
`ToListAsync`) also keep an `IQueryable<T>`-typed fallback overload for LINQ operators that degrade
the static type away from `IGraphQueryable<T>` (see §7).

## 4. `IGraphQueryable` no longer implements `IAsyncDisposable`

It was a no-op (`DisposeAsync() => ValueTask.CompletedTask`) — nothing ever needed disposing.
Remove any `await using`/`DisposeAsync()` calls on queryables; there is nothing to migrate to,
because there was nothing being disposed.

## 5. `WithDepth`/`Direction` free-floating modifiers are removed — fold them into the traversal call

`WithDepth(...)` and `Direction(...)` as standalone postfix operators on any `IGraphQueryable<T>`
have been removed. Their problem: nothing in
the type system enforced that they followed a traversal operator — `graph.Nodes<Person>().WithDepth(3)`
compiled and did nothing. Use the depth/direction overloads on `Traverse`/`TraversePaths` instead,
or the options-lambda overload:

```diff
-graph.Nodes<Person>().Traverse<Knows, Person>().WithDepth(1, 3)
+graph.Nodes<Person>().Traverse<Knows, Person>(1, 3)
# or, combining depth and direction:
+graph.Nodes<Person>().Traverse<Knows, Person>(o => o.Depth(1, 3).Direction(GraphTraversalDirection.Incoming))
```

`PathSegments<TStart, TRel, TEnd>(direction)` is the canonical replacement when a directed
single-hop result must retain its start node, relationship, and end node. Use `Traverse` when only
the endpoint is needed, or `TraversePaths` when the result must retain a variable-length path.

## 6. Multi-hop traversal: `TraversePaths` returns `IGraphPath`, not more `IGraphPathSegment`s

Previously, calling `.WithDepth(2)` (or any range beyond a single hop) after `PathSegments<S,R,T>()`
compiled but had undefined semantics — a single `IGraphPathSegment<S,R,T>` cannot represent more
than one hop, so what ended up in `Relationship`/intermediate nodes at depth > 1 was
provider-defined, not part of the contract.

Variable-length traversal is now an explicit, separate operator: `TraversePaths` returns
`IGraphQueryable<IGraphPath>`, where `IGraphPath` exposes `Start`, `End`, and an ordered
`Segments: IReadOnlyList<IGraphPathSegment>` (one entry per hop):

```csharp
var paths = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .TraversePaths<Knows, Person>(minDepth: 1, maxDepth: 3)
    .ToListAsync();

foreach (var path in paths)
{
    Console.WriteLine($"{path.Start.Id} -> {path.End.Id} via {path.Segments.Count} hop(s)");
}
```

An options-lambda overload is also available: `TraversePaths<TRel, TEnd>(o => o.Depth(1, 3).Direction(...))`.

If you only need the end nodes of a variable-length traversal (not the full path), continue using
`Traverse<TRel, TEnd>(minDepth, maxDepth)` (see §7 below for its new two-arg shape).

**Settled behavior:** entities exposed through `Start`, `End`, and every entry in `Segments` now
hydrate complex properties with the same read shape as `PathSegments`. The provider keeps one row
per matched path while composing `.Where(...)`, `.Select(...)`, `.Take(...)`, `.Skip(...)`,
`.CountAsync()`, and `.AnyAsync()`, then decomposes only path-valued results into materialization
rows. Path predicates can inspect `Start`/`End` members and depth through `Segments.Count`:

```csharp
var results = await graph.Nodes<Person>()
    .TraversePaths<Knows, Person>(1, 2)
    .Where(path => ((Person)path.End).Age > 35 && path.Segments.Count == 2)
    .Take(10)
    .ToListAsync();
```

Operator order matters: the query model applies path predicates before projection and pagination,
so a `.Where(...)` placed after `.Select(...)`, `.Take(...)`, or `.Skip(...)` throws at translation
time rather than silently filtering a different row set than LINQ semantics require — put filters
first. `.Select(path => path)` is a projection no-op.

The single translation guard remains in place for operators without a path-row lowering, such as
`.OrderBy(...)`: those still throw a `NotSupportedException` naming the operator instead of
silently translating against a hop row. Materialize first and continue with LINQ-to-Objects when
using an unsupported composition.

## 7. `Traverse`/`TraverseRelationships`/`TraversePaths`/`ReverseTraverse` drop `TStartNode` — two type arguments, not three

**This is the "Option C" decision from issue #94** (recorded on the issue as the final pick,
superseding an earlier three-explicit-type-argument carry-forward and a rejected
`Via<TRel>().To<TEnd>()` builder alternative). `Traverse`, `TraverseRelationships`, `TraversePaths`,
and `ReverseTraverse` now take only `TRel` and `TEnd` as explicit type arguments — `TStartNode` is
gone:

```diff
-IGraphQueryable<TEnd> Traverse<TStartNode, TRelationship, TEndNode>(this IGraphQueryable<TStartNode> source, ...)
-    where TStartNode : INode where TRelationship : IRelationship where TEndNode : INode;
+IGraphQueryable<TEnd> Traverse<TRel, TEnd>(this IGraphQueryable<INode> source, ...)
+    where TRel : class, IRelationship where TEnd : class, INode;
```

```diff
-graph.Nodes<Person>().Traverse<Person, Knows, Person>()
+graph.Nodes<Person>().Traverse<Knows, Person>()
```

**Why this is safe, and why it's more than cosmetic.** `IGraphQueryable<T>` is covariant
(`IGraphQueryable<out T>`), so any `IGraphQueryable<TStartNode>` where `TStartNode : INode` converts
to `IGraphQueryable<INode>` at the call site — the start type rides in on the receiver instead of a
generic slot. This isn't just shorter to type: under the old three-arg shape, `TStartNode` was
*always* redundant with the source's actual element type, but nothing enforced that — writing
`.Traverse<Person, Knows, Manager>()` against an `IGraphQueryable<Employee>` source (where `Manager`
happens to be a valid `INode` unrelated to `Employee`) compiled with no diagnostic tying `TStartNode`
back to the receiver, silently mis-describing the traversal's start. The two-arg shape has no
`TStartNode` slot to mistype; the actual start type is recovered by the provider from the source
expression chain's element type at translation time, not trusted from a caller-supplied argument.

`PathSegments<TStart, TRel, TEnd>()` is the one exception and **keeps all three type arguments**:
its result type is `IGraphPathSegment<TStart, TRel, TEnd>`, which does name the start type, so it
remains a required, explicit argument there. The stated principle: spell exactly the types that
appear in the result type. `Traverse`/`TraverseRelationships`/`TraversePaths` return
`TEnd`/`TRel`/`IGraphPath` — none mention the start type — so they go two-arg.

**Migration is mechanical:** drop the first (start node) type argument from every
`Traverse`/`TraverseRelationships`/`TraversePaths`/`ReverseTraverse` call. The three-arg forms are
removed after their one-release migration window.

**Caveat — struct entity types.** Variance conversions don't apply to value types, so a `struct`
implementing `INode` cannot call the two-arg form (`IGraphQueryable<StructNode>` does not convert to
`IGraphQueryable<INode>`). This is not expected to affect real usage: entity types are expected to be
reference types (see §7a below).

## 7a. Entity type parameters now require `class`

Every `where T : INode` / `where T : IRelationship` generic constraint across the query surface
(query roots, LINQ operators, CRUD/transaction APIs, serialization generics) is now
`where T : class, INode` / `where T : class, IRelationship`. `IRelationship<TSource, TTarget>` already required
`class` (plus `new()`) on its node type parameters; this extends the same "entities are reference
types" rule to every other generic entity constraint. If your domain models are `record`/`class`
types (the norm, and what every in-tree example and analyzer rule assumes), this changes nothing at
your call sites. A `struct` implementing `INode`/`IRelationship` at a generic entity type parameter
will now fail to compile; a companion declaration-site analyzer rule (`CG014`, tracked in a separate
issue) flags struct entity type declarations directly.

## 7b. `ReverseTraverse` — also fixed inverted type-parameter order

Independent of the two-arg reshape above, `ReverseTraverse`'s type parameters used to be inverted
relative to every other traversal operator: the old three-arg signature was
`ReverseTraverse<TStartNode, TRelationship, TEndNode>(this IGraphNodeQueryable<TEndNode> source)` —
i.e. the receiver's element type bound to `TEndNode`, not `TStartNode`, which was backwards compared
to `Traverse<TStartNode, TRelationship, TEndNode>(this IGraphQueryable<TStartNode> source)`. The
current (two-arg) signature is consistent with every other traversal operator: the receiver's
element type is the start type, recovered by covariance rather than named at all.

Call sites did not change syntactically (`source.ReverseTraverse<Knows, Person>()` reads the same
either way when start and end are the same type), but if you had a three-arg call with distinct
start/end types, double-check which one you're passing when you migrate from the removed shim — the
roles are no longer swapped.

## 8. `Search` unification: `IGraph.Search*Async` are now synchronous, renamed, and delegate to `.Search()`

`IGraph.SearchAsync`, `SearchNodesAsync`, `SearchNodesAsync<T>`, `SearchRelationshipsAsync`, and
`SearchRelationshipsAsync<T>` are renamed (drop the `Async` suffix, since building the queryable is
synchronous like every other root) and are now thin conveniences over the `.Search()` LINQ operator
— `graph.SearchNodes<T>(query)` is exactly `graph.Nodes<T>().Search(query)`.

```diff
-var results = await (await graph.SearchNodesAsync<Article>("machine learning")).ToListAsync();
+var results = await graph.SearchNodes<Article>("machine learning").ToListAsync();
```

| Old | New |
|---|---|
| `Task<IGraphQueryable<IEntity>> SearchAsync(query, tx = null)` | `IGraphQueryable<IEntity> Search(query, tx = null)` |
| `Task<IGraphNodeQueryable<INode>> SearchNodesAsync(query, tx = null)` | `IGraphQueryable<INode> SearchNodes(query, tx = null)` |
| `Task<IGraphNodeQueryable<T>> SearchNodesAsync<T>(query, tx = null)` | `IGraphQueryable<T> SearchNodes<T>(query, tx = null)` |
| `Task<IGraphRelationshipQueryable<IRelationship>> SearchRelationshipsAsync(query, tx = null)` | `IGraphQueryable<IRelationship> SearchRelationships(query, tx = null)` |
| `Task<IGraphRelationshipQueryable<T>> SearchRelationshipsAsync<T>(query, tx = null)` | `IGraphQueryable<T> SearchRelationships<T>(query, tx = null)` |

The `.Search(query)` LINQ operator itself (usable anywhere in a chain, e.g.
`graph.Nodes<Person>().Where(...).Search(...)`) is unchanged.

## 9. Internal marker surface renamed (only relevant if you referenced it via reflection)

The internal `QueryableAsyncExtensionsMarkers` class is renamed to `QueryTerminals` and its
numeric `Sum`/`Average` marker overloads are collapsed from one-per-numeric-type to a single
generic definition per shape. This type was never public API (`internal`), so this only matters if
you were reflecting into it directly (e.g. a custom provider or test harness) — update the type
name and, if you built `SumAsyncMarker`/`AverageAsyncMarker` calls by matching a specific numeric
`MethodInfo` overload, match the single generic definition instead. Close `SumAsyncMarker` over
its numeric input/result type. Close `AverageAsyncMarker` over its numeric **input** type; integer
and long averages now materialize as `double`, so the LINQ result type is carried separately by the
provider's `ExecuteAsync<TResult>` call.

The public `AverageAsync` surface now follows the LINQ overload matrix instead of accepting any
`INumberBase<T>`: `int` and `long` return `double`, `float` returns `float`, `double` returns
`double`, and `decimal` returns `decimal`, with nullable inputs returning the corresponding nullable
result. Non-nullable empty sequences throw `InvalidOperationException`; nullable empty or all-null
sequences return `null`. Calls over other numeric types no longer compile.

If you're building a provider, see
[`docs/provider-implementers-guide.md`](provider-implementers-guide.md) for the updated
`MethodInfo`-identity dispatch table your provider should implement (previously string-name
dispatch).

## 10. Neo4j provider: `DynamicEntityExtensions` renamed to `Neo4jDynamicEntityExtensions`

The Neo4j package's public `DynamicEntityExtensions` class (namespace `Cvoya.Graph.Neo4j`)
is renamed to `Neo4jDynamicEntityExtensions` to resolve the name collision with the core
package's `Cvoya.Graph.DynamicEntityExtensions`. The class stays in the Neo4j package
(its accessors are genuinely driver-coupled via the serialization bridge) and its extension
methods (`GetProperty<T>`, `HasLabel`, `HasType`, `GetPropertyNames`, `HasProperty`,
`HasAnyLabel`, `HasAllLabels`) are unchanged.

**Impact:** extension-method call sites (`node.GetProperty<int>("age")`) are source-compatible —
they resolve by namespace, not class name. You only need a change if you referenced the class
*by name*, i.e. static-style invocation (`DynamicEntityExtensions.GetProperty<int>(node, "age")`
→ `Neo4jDynamicEntityExtensions.GetProperty<int>(node, "age")`) or reflection by type name.

## 11. Cancellation is now propagated and streaming is incremental

`IGraph.GetTransactionAsync` now accepts an optional `CancellationToken`:

```csharp
await using var transaction = await graph.GetTransactionAsync(cancellationToken);
```

This is source-compatible for normal calls because the parameter is optional, but it is a binary
change for consumers compiled against the previous pre-1.0 API.

Cancellation tokens are now checked and propagated through the Neo4j provider's transaction,
CRUD/schema, query execution, result-processing, and materialization paths. A pre-cancelled or
cancelled operation now surfaces `OperationCanceledException`; it is no longer converted into an
empty result or wrapped in `GraphException`.

`await foreach` over a Neo4j `IGraphQueryable<T>` now consumes the driver cursor incrementally.
Buffered terminal operators such as `ToListAsync` still buffer by design. If an auto-transaction
owned by the streaming query is abandoned before enumeration completes, the provider rolls it back
and disposes it; caller-owned ambient transactions remain caller-owned.

## 12. API contract cleanup: graph lifetime, transaction rollback, and missing entities

`IGraph` no longer implements `IAsyncDisposable`; graph instances are facades over provider-owned
resources. Dispose the provider store that created the graph instead:

```diff
-await graph.DisposeAsync();
+await store.DisposeAsync();
```

`Neo4jGraphStore` owns and disposes the driver only when constructed from URI/username/password.
When constructed with an external `IDriver`, the caller keeps driver ownership and must dispose it.

`IGraphTransaction.Rollback()` is renamed to `RollbackAsync()`:

```diff
-await transaction.Rollback();
+await transaction.RollbackAsync();
```

Missing entities from get/update/delete operations now throw `EntityNotFoundException`, which
derives from `GraphException`, instead of relying on the broader graph exception contract.

## 13. `RelationshipDirection.Bidirectional` is removed

`RelationshipDirection` now has only `Outgoing` and `Incoming`, and it describes storage direction
at rest. `Outgoing` stores the physical edge as `StartNodeId -> EndNodeId`; `Incoming` stores it as
`EndNodeId -> StartNodeId`. There is no undirected stored relationship shape in Neo4j, so
`RelationshipDirection.Bidirectional` no longer compiles:

```diff
-new Knows { StartNodeId = a.Id, EndNodeId = b.Id, Direction = RelationshipDirection.Bidirectional }
+new Knows { StartNodeId = a.Id, EndNodeId = b.Id }
```

If you need to traverse relationships in either direction, keep the stored relationship directed
and choose both-direction traversal at query time:

```csharp
var connected = await graph.Nodes<Person>()
    .Where(p => p.Id == personId)
    .Traverse<Knows, Person>(GraphTraversalDirection.Both)
    .ToListAsync();
```

Pre-existing stored data written by older CVOYA graph versions with `Direction = Bidirectional` is
not rewritten. The Neo4j provider ignores unrecognized relationship direction values on read and
materializes them as `RelationshipDirection.Outgoing`, so legacy data remains readable but no
longer represents a bidirectional storage contract.

## 14. `FirstAsync` and `SingleAsync` now throw on empty query results

Graph query terminals now match LINQ-to-Objects element semantics for empty and multiple-result
sources. `FirstAsync` and `SingleAsync` throw `InvalidOperationException` when the query returns no
rows; earlier provider materialization could return `default` for those empty scalar terminals.
`SingleAsync` and `SingleOrDefaultAsync` also throw `InvalidOperationException` when more than one
row matches. `FirstOrDefaultAsync` and `SingleOrDefaultAsync` still return `default` for empty
sources.

## 15. Relationship direction changes now fail during update

Updating a persisted relationship with a different `Direction` now throws `GraphException`:
`Direction cannot be changed on update; delete and recreate the relationship`. Property updates
with the same `Direction` continue to work. To reverse the stored edge direction, delete and
recreate the relationship.

## 16. Complex properties are now first-class graph structure

Complex properties no longer use reserved, mangled relationship names or an APOC-based hidden
reconstruction query. A property such as `Person.HomeAddress` now persists as
`(:Person)-[:HomeAddress]->(:Address)`. Use
`[ComplexProperty(RelationshipType = "LIVES_AT")]` to override the relationship type.

This is a breaking storage change. Existing databases must either be rebuilt from the source of truth or
migrated before the new provider reads them. A migration should, for every legacy complex-property edge:

1. derive the owning CLR property (including any new attribute override),
2. create the semantic relationship type,
3. preserve collection `SequenceNumber`,
4. assign CVOYA graph IDs to the value node and relationship, and
5. add the provider's complex-property marker used for bounded recursive loading and cascade cleanup.

Do not keep mixed old and new representations: the new query planner only follows semantic relationship
types. Declared properties auto-load recursively to `GraphDataModel.DefaultDepthAllowed` (5); deeper
object graphs and cycles now fail before writing. Each property occurrence is stored as a distinct value
node, even if multiple owners reference the same in-memory instance.

## 17. Complex-property navigation is null-propagating

Complex-property navigation in queries (`p.Address.City`) now lowers to `OPTIONAL MATCH`, so rows
without the navigated complex property stay in the result set: OR-predicates keep them in play,
and projections/orderings yield `null` for the missing value instead of silently dropping the row.
Consequently `p.Address.City == null` matches both "no `Address` node" and "`City` is null" — use
`p.Address == null` when you specifically mean "no `Address` node". Decision recorded on #221;
revisit tracked in #233. If you relied on the old required-`MATCH` behavior (navigation implicitly
filtering out rows without the complex property), add an explicit `p.Address != null` predicate.

## 18. `TerminalOperation.Distinct` is removed from the query model

`Distinct` was both a `TerminalOperation` member and the `GraphQueryModel.Distinct` flag; only the
flag remains, so `Distinct().CountAsync()` can carry both semantics at once (#210). Hand-built
models that used `TerminalOperation.Distinct` should set `distinct: true` and let the terminal
describe the actual terminal operation. `ElementAt`/`ElementAtOrDefault` models now carry their
index in `GraphQueryModel.TerminalOperand` instead of encoding it as `Paging`.

## 19. `RelationshipPattern.Type` is now `Types`

`Cvoya.Graph.Cypher.Ast.RelationshipPattern` carries relationship type names as
`IReadOnlyList<string> Types` instead of a single pre-joined `Type` string (#214). Renderers join
alternatives with `|` and escape each name, so a literal `|` or backtick in a type name is part of
the name. Pass each type as its own list entry to the multi-type constructor
`(alias, direction, depth, types)`; the original single-`string` constructor overload remains for
one-type and any-type patterns, including an uncast `null` type argument.

## 20. Cypher planning and result materialization require explicit provider seams

`CypherQueryPlanner` now requires an `ICypherDialect`; its public parameterless constructor is removed.
Custom Cypher providers should pass the same dialect to the shared planner and renderer:

```diff
-var statement = new CypherQueryPlanner().Plan(model);
+var statement = new CypherQueryPlanner(dialect).Plan(model);
+var rendered = new CypherRenderer(dialect).Render(statement);
```

The renderer returns `CypherRenderResult` rather than a provider-local query record. Besides text and
parameters, it exposes the exact projection columns required by SQL-wrapped Cypher engines such as Apache
AGE.

Provider result code should adapt driver records into `GraphRecord`/`GraphValue` and call
`GraphResultMaterializer`. Driver node/relationship interfaces are no longer accepted by the shared
materialization pipeline. This is an implementation-SPI change only; application LINQ queries and stored
graph data require no migration, and Neo4j query text remains byte-for-byte unchanged.

## 21. `PropertyAttribute.Label` is the physical storage key

`[Property(Label = "last_name")]` now stores, indexes, queries, dynamically exposes, and
materializes the property as `last_name`. Earlier versions generated serializers that stored the
same property under its CLR name (`LastName`) even though schema and LINQ metadata resolved the
configured label. Data written under the old key is not found by label-keyed queries and is not
automatically migrated.

Migrate each renamed property before upgrading, substituting the affected node label and property
names in this Cypher template:

```cypher
MATCH (n:`NodeLabel`)
WHERE n.`ClrPropertyName` IS NOT NULL
SET n.`physical_property_label` = n.`ClrPropertyName`
REMOVE n.`ClrPropertyName`;
```

Use the equivalent relationship pattern (`MATCH ()-[r:`RELATIONSHIP_TYPE`]->()`) and replace `n`
with `r` for renamed relationship properties. Rebuild indexes after migrating so their property
keys also use the configured labels.

## 22. Paged filters and string overload arguments are no longer silently reordered or ignored

`Where`, `OrderBy`, and `Distinct` operators that follow `Skip` or `Take` now run against that
paged window. For example, `query.Take(5).Where(predicate)` first selects five rows and then
filters those rows; earlier versions moved the predicate before `Take` and could return a
different result set. Custom providers consuming `GraphQueryModel` should apply its optional
`PostPaging` stage after the primary `Paging` window. A later paging window may finish that stage,
but another sequence operator after it requires materializing the query first. Predicate-bearing
terminal overloads after paging likewise require materialization instead of being silently moved
ahead of the paging window.

String-method overload arguments are also preserved. `Contains`, `StartsWith`, `EndsWith`, and
`Replace` support `StringComparison.Ordinal`. Ignore-case and culture-sensitive comparisons,
culture-sensitive casing, non-ordinal comparison-aware `Replace`, and custom-character
`Trim`/`TrimStart`/`TrimEnd` overloads now throw `GraphQueryTranslationException` because the
shared Cypher dialect layer cannot represent their .NET semantics faithfully. Queries that
appeared to use those overloads previously had their extra arguments silently discarded and
should be rewritten to a supported ordinal comparison or evaluated client-side.

## 23. `ICypherDialect` owns full-text clause rendering

This affects only custom Cypher **providers** (implementers of the `Cvoya.Graph.Cypher` SPI); it
has no effect on application code. The four full-text name members on `ICypherDialect` —
`FullTextNodeProcedure`, `FullTextRelationshipProcedure`, `FullTextNodeIndex`, and
`FullTextRelationshipIndex` — are **removed**. The shared `CypherRenderer` no longer hard-codes the
`CALL <proc>(<index>, <query>) YIELD …` scaffolding or the mixed-entity `CALL { … UNION ALL … }`
subquery; instead it delegates the whole clause to the dialect:

```csharp
string RenderFullTextSearch(FullTextSearchClause clause, ICypherRenderContext context);
```

The member has a default implementation that throws `GraphQueryTranslationException`, so a dialect
that does not declare `GraphCapability.FullTextSearch` needs no change. A dialect that **does**
support full-text search moves its procedure/index names and its clause shape into an override of
`RenderFullTextSearch`, rendering expressions and literals through the supplied
`ICypherRenderContext`. The rendered Cypher is byte-for-byte identical to previous versions; this is
a pure relocation of where the clause is rendered, completing the dialect-abstraction work begun in
#215.

## 24. Relationship predicates and existence filters are server-side graph operators

Relationship filtering no longer requires exposing a one-hop `PathSegments` result and filtering it
afterward. Configure `Traverse` or `TraversePaths` with
`options.WhereRelationship<TRel>(predicate)`; every relationship in a variable-length path must
match. Use `WhereHasRelationship<TNode, TRel>(direction, predicate?)` when only relationship
existence matters and the concrete node type should remain in the query chain. The
`WhereHasRelationship<TRel>` convenience overload returns `IGraphQueryable<INode>`.

Custom providers must declare `GraphCapability.RelationshipPredicates` only after implementing both
expansion-time predicates and anchored existence patterns. A provider that does not declare the
capability now rejects these operators during translation rather than ignoring them or filtering
materialized results on the client. Apply `WhereHasRelationship` before `Select`, `Skip`, or `Take`;
the shared validator rejects the reverse order. AGE deliberately declines this capability. No
stored-data migration is required.

## 25. Typed shortest-path traversal

Use `ShortestPath<TRel, TEnd>(endpointPredicate?, direction?)` to return one minimum-hop path per
source/endpoint pair, or `AllShortestPaths<TRel, TEnd>(endpointPredicate?, direction?)` to retain
all equal-length ties. Both return `IGraphQueryable<IGraphPath>`, infer the start type from the
source, require at least one hop, and exclude the source itself as an endpoint.

Custom providers must declare `GraphCapability.ShortestPath` only after implementing the gated
compatibility contract. A provider that does not declare it rejects either operator at translation
time. No stored-data migration is required.

## 26. Public optional traversal

`OptionalTraverse<TRel, TEnd>(direction?)` now exposes the provider-neutral left-traversal surface.
It returns `OptionalTraversalResult<TEnd>` with the preserved `INode Source` and nullable
`TEnd Target`. Filter source rows before the operator (predicates, label filters,
relationship-existence filters, and full-text search all compose there); projection and paging may
follow it. `Where`, `OrderBy`, `Distinct`, and aggregate terminals over the optional result are
rejected at the shared model boundary, as is combining the operator with another traversal.

Providers already declaring `GraphCapability.OptionalTraversal` must implement this public shape in
addition to complex-property null propagation. Providers that cannot preserve an unmatched source
row must stop declaring the capability. No stored-data migration is required.

## 27. Typed `Union` and bag-preserving `Concat`

`IGraphQueryable<T>` now has graph-typed `Union` and `Concat` overloads. `Union` removes duplicate
rows; `Concat` preserves them and lowers to `UNION ALL`. Compatible node and scalar projection
operands are supported. Same-kind left chains such as `a.Union(b).Union(c)` and
`a.Concat(b).Concat(c)` are supported. A flat chain cannot mix `Union` and `Concat`; nest the second
operand to declare grouping or materialize the first operation. Other sequence operators still
require materializing the combined query first.

Custom providers must declare `GraphCapability.SetOperations` only after implementing both
distinct and bag-preserving behavior with isolated operand parameters. AGE deliberately declines
the new capability. No stored-data migration is required.

## 28. Typed and dynamic label filters

Node queries now expose `OfLabel(label)` and `OfLabels(match, labels)`. `GraphLabelMatch.Any`
requires at least one requested stored label; `GraphLabelMatch.All` requires every requested label.
Passing no labels to `OfLabels` leaves the query unchanged. Both operators preserve the
`IGraphQueryable<TNode>` chain and compose with node predicates, ordering, projection, and paging
when applied before those stages. A label filter after `Select`, `Skip`, or `Take` is rejected at the
shared model boundary rather than being silently reordered.

Custom providers must declare `GraphCapability.LabelFiltering` only after implementing value-safe
label tests over typed and dynamic node scopes. Label strings must never be interpolated as query
identifiers. A provider that does not declare the capability rejects non-empty label filters during
translation. No stored-data migration is required.

## Non-changes (things that look related but aren't)

- `.Search(query)` as a LINQ operator on `IGraphQueryable<T>` — unchanged.
- `PathSegments<TStart, TRel, TEnd>()` (the single-hop primitive) and its three type arguments —
  unchanged; use its new `direction` overload instead of the removed free-floating modifier (see
  §7 for why `PathSegments` keeps `TStart` while `Traverse` and friends drop it).
- The depth/direction overload *shapes* on `Traverse`/`TraversePaths` (no-args, `maxDepth`,
  `minDepth, maxDepth`, `direction`, options-lambda) — unchanged; only the type-argument count
  changed (§7), not which overloads exist.

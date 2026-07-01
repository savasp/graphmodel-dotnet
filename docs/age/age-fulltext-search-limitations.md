# Apache AGE Full-Text Search Limitations

## Overview

Apache AGE (1.7.x) does **not** provide a native full-text search (FTS) capability within
its Cypher implementation. Unlike Neo4j which offers `db.index.fulltext.*` procedures,
AGE has no equivalent server-side index for searching across all string properties of
nodes or relationships.

The GraphModel library's FTS feature (`IGraph.SearchNodesAsync<T>("query")`) relies on
the database provider to efficiently search **all string properties** of an entity for a
given term. This document explains why this is not currently achievable in AGE and what
workarounds exist.

## Root Cause

### 1. No Cypher-Level Full-Text Search Functions

AGE's Cypher implementation supports basic string matching operators (`=~`, `STARTS WITH`,
`CONTAINS`, `ENDS WITH`) but has **no** dedicated full-text search indexes or functions:

| Feature | AGE 1.7 | Neo4j |
|---|---|---|
| Server-side FTS index | ❌ | ✅ (`db.index.fulltext.*`) |
| `toString(vertex)` | ❌ (fails with "unsupported argument agtype") | ✅ |
| `coalesce(vertex.prop, '')` in WHERE | ⚠️ Partially — fails with "agtype string values expected" for non-string properties | ✅ |
| Multi-label edge matching (`:Label1\|Label2`) | ❌ (syntax error) | ✅ |

### 2. Vertex/Edge Properties Stored as Opaque agtype

AGE stores all vertex/edge properties in a single `agtype` column (a custom JSON-like
PostgreSQL type). Properties are **not** individual SQL columns, so PostgreSQL's native
`tsvector`/`tsquery` indexes cannot be created on them directly.

### 3. Missing `toString()` for Vertices

The most natural approach would be:
```cypher
MATCH (n) WHERE toString(n) =~ '(?i).*searchTerm.*' RETURN n
```

This fails with: `toString() unsupported argument agtype`

### 4. `coalesce()` Incompatibility with `=~`

For interface types (INode, IRelationship) or inheritance queries, the ideal approach
would use `coalesce()`:
```cypher
MATCH (n:Person)
WHERE coalesce(n.Department, '') =~ '(?i)\mEngineering\M'
```

This fails when `Department` exists on some nodes as a non-string type with:
`agtype string values expected`

## Skipped Tests

The following tests are skipped for the AGE provider due to these limitations:

| Test | Reason |
|---|---|
| `CanSearchAllEntitiesWithFullTextSearch` | Label-less MATCH for IEntity picks up all vertices, no FTS index |
| `CanSearchRelationshipsWithGenericInterface` | Label-less edge MATCH for IRelationship returns too many results |
| `CanSearchInPathSegmentsChain` | FTS in path segments cannot use `coalesce()` with `=~` |
| `CanSearchDynamicRelationshipWithFullTextSearch` | DynamicRelationship FTS uses label-less MATCH |

## Example Queries That Do Not Work

### ❌ Searching all entities (IEntity)
```csharp
// This works in Neo4j but not in AGE:
var results = await (await Graph.SearchAsync("SearchUser")).ToListAsync();
// → AGE: no way to search across all node types without a label
```

### ❌ Searching with generic interface (INode/IRelationship)
```csharp
// Works in Neo4j, fails in AGE:
var nodes = await (await Graph.SearchNodesAsync("Wonder")).ToListAsync();
// → AGE: INode has no concrete label, can't match nodes
```

### ❌ Searching relationships generically
```csharp
// Works in Neo4j, fails in AGE:
var rels = await (await Graph.SearchRelationshipsAsync("unique_term")).ToListAsync();
// → AGE: IRelationship has no concrete label, label-less MATCH picks up too many
```

### ❌ Searching in path segment chains
```csharp
// Works in Neo4j, fails in AGE:
var results = await (await Graph.NodesAsync<Person>())
    .Where(u => u.Id == user.Id)
    .PathSegments<Person, KnowsWell, Person>()
    .Select(p => p.EndNode)
    .Search("vacation")
    .ToListAsync();
// → AGE: coalesce() on vertex props in WHERE fails with =~
```

### ❌ Inheritance search
```csharp
// Searching for "Construction" on Manager.Department:
await Graph.SearchNodesAsync<Person>("Construction").ToListAsync();
// → AGE: Person doesn't have Department property; coalesce() approach fails
```

## Workarounds

### 1. Per-Property Searching (CURRENTLY SUPPORTED)
The current AGE provider implements FTS as per-property `=~` checks on concrete types:
```cypher
MATCH (src0:Person)
WHERE src0.FirstName =~ '(?i)\mJohn\M' OR src0.LastName =~ '(?i)\mJohn\M'
RETURN src0
```
This works for **concrete types with known string properties** (e.g., `SearchNodesAsync<Person>("John")`).

### 2. PostgreSQL-Level Implementation (FUTURE)
A potential future implementation could use PostgreSQL's native `tsvector`/`tsquery`:
- Create a helper function to convert `agtype` properties to text
- Add generated `tsvector` columns to the underlying `_ag_label_vertex` table
- Query via raw SQL, then fetch entities by ID via Cypher
- This is a significant architectural change — see the [AGE FTS Architecture document](./age-fulltext-search-architecture.md)

### 3. Use Concrete Types Always
Until server-side FTS is implemented, always search with concrete types:
```csharp
// ✅ Works:
await Graph.SearchNodesAsync<Person>("John").ToListAsync();

// ❌ Does not work:
await Graph.SearchNodesAsync("John").ToListAsync(); // INode
await Graph.SearchAsync("John").ToListAsync(); // IEntity
```

## Related Issues

- Apache AGE does not have an open GitHub issue or roadmap item for full-text search
  as of May 2026.
- The [AGE Cypher operators documentation](https://age.apache.org/age-manual/master/intro/operators.html)
  confirms that only `=~`, `STARTS WITH`, `CONTAINS`, and `ENDS WITH` are supported.
- https://github.com/apache/age/issues/1639
- https://gist.github.com/mingfang/729e70e819b2bacabb6519c32fd761cd

## References

- [AGE Operator Documentation](https://age.apache.org/age-manual/master/intro/operators.html)
- [AGE Graph Storage Internals](https://age.apache.org/age-manual/master/intro/graphs.html)
- [GraphModel Full-Text Search Architecture](./age-fulltext-search-architecture.md)
- Test file: `tests/Graph.Model.Age.Tests/GraphModelTests/FullTextSearchTests.cs`

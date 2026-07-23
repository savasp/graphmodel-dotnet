# Example 2: LINQ and traversal

This example demonstrates typed filtering, ordering, traversal, and path-segment queries with the
Neo4j provider.

## Query roots and terminals

Query roots are synchronous. Async terminals execute them:

<!-- checked-snippet: examples/Example2.LINQAndTraversal/DocumentationSnippets.cs#query-roots -->
```csharp
var newYorkers = await graph.Nodes<Person>()
    .Where(person => person.City == "New York")
    .OrderBy(person => person.Name)
    .ToListAsync();

var ageRange = await graph.Nodes<Person>()
    .Where(person => person.Age >= 28 && person.Age <= 32)
    .ToListAsync();
```

## Traversal

`Traverse` returns reached nodes. Its optional depth argument controls the number of hops:

<!-- checked-snippet: examples/Example2.LINQAndTraversal/DocumentationSnippets.cs#traversal -->
```csharp
var directConnections = await graph.Nodes<Person>()
    .Where(person => person.Name == "Alice")
    .Traverse<Knows, Person>()
    .ToListAsync();

var twoHopConnections = await graph.Nodes<Person>()
    .Where(person => person.Name == "Alice")
    .Traverse<Knows, Person>(2)
    .ToListAsync();
```

Use path segments when relationship data, endpoints, or physical edge orientation matters:

<!-- checked-snippet: examples/Example2.LINQAndTraversal/DocumentationSnippets.cs#path-segments -->
```csharp
var connections = await graph.Nodes<Person>()
    .Where(person => person.Name == "Alice")
    .PathSegments<Person, Knows, Person>()
    .ToListAsync();
```

Each segment exposes `StartNode`, `Relationship`, `EndNode`, and `Direction`. The relationship
object itself has no endpoint or direction fields.

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example2.LINQAndTraversal
```

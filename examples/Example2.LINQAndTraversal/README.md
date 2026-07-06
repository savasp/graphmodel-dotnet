# Example 2: LINQ and Traversal

This example demonstrates LINQ querying capabilities and graph traversal with depth control using the GraphModel library.

## What You'll Learn

- Using LINQ to query graph data
- Controlling traversal depth when loading relationships
- Working with navigation properties
- Complex queries involving multiple levels of relationships
- Finding patterns in graph data

## Key Concepts Demonstrated

### 1. LINQ Queries

```csharp
// Filter by property
var results = await (await graph.NodesAsync<Person>())
    .Where(p => p.City == "New York")
    .OrderBy(p => p.Name)
    .ToListAsync();

// Range queries
var ageRange = await (await graph.NodesAsync<Person>())
    .Where(p => p.Age >= 28 && p.Age <= 32)
    .ToListAsync();
```

### 2. Traversal Depth Control

```csharp
// Immediate relationships
var directConnections = await (await graph.NodesAsync<Person>())
    .Where(p => p.Name == "Alice")
    .Traverse<Knows, Person>(1)
    .ToListAsync();

// Up to two hops
var extendedConnections = await (await graph.NodesAsync<Person>())
    .Where(p => p.Name == "Alice")
    .Traverse<Knows, Person>(1, 2)
    .ToListAsync();
```

### 3. Navigation Properties

```csharp
var pathSegments = await (await graph.NodesAsync<Person>())
    .Where(p => p.Name == "Alice")
    .PathSegments<Person, Knows, Person>()
    .ToListAsync();
```

### 4. Complex Relationship Queries

```csharp
// Find people who know someone in a specific city
var results = await (await graph.NodesAsync<Person>())
    .Traverse<Knows, Person>(1)
    .Where(p => p.City == "San Francisco")
    .ToListAsync();
```

## Running the Example

```bash
cd examples/Example2.LINQAndTraversal
dotnet run
```

Make sure Neo4j is running and accessible at `neo4j://localhost:7687`.

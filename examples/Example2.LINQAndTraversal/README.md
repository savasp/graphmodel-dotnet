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
var results = graph.Nodes<Person>()
    .Where(p => p.City == "New York")
    .OrderBy(p => p.Name)
    .ToList();

// Range queries
var ageRange = graph.Nodes<Person>()
    .Where(p => p.Age >= 28 && p.Age <= 32)
    .ToList();
```

### 2. Traversal Depth Control

```csharp
// No relationships loaded
var node = await graph.GetNode<Person>(id, new GraphOperationOptions().WithDepth(0));

// Load immediate relationships
var node = await graph.GetNode<Person>(id, new GraphOperationOptions().WithDepth(1));

// Load two levels of relationships
var node = await graph.GetNode<Person>(id, new GraphOperationOptions().WithDepth(2));
```

### 3. Navigation Properties

```csharp
public IEnumerable<Knows> Knows => GetRelationships<Knows>();
```

### 4. Complex Relationship Queries

```csharp
// Find people who know someone in a specific city
var results = graph.Nodes<Person>(new GraphOperationOptions().WithDepth(1))
    .Where(p => p.Knows.Any(k => k.Target != null && k.Target.City == "San Francisco"))
    .ToList();
```

## Running the Example

```bash
cd examples/Example2.LINQAndTraversal
dotnet run
```

Make sure Neo4j is running and accessible at `neo4j://localhost:7687`.

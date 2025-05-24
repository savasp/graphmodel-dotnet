# Example 1: Basic CRUD Operations

This example demonstrates the fundamental Create, Read, Update, Delete (CRUD) operations using the GraphModel library with Neo4j.

## What You'll Learn

- How to define domain models using `Node` and `Relationship` records
- Creating nodes and relationships in the graph
- Querying data using LINQ-to-Cypher
- Updating existing graph data
- Deleting nodes and relationships
- Using C# 13 record types with GraphModel

## Domain Model

The example uses a simple organizational structure:

- **Person**: Represents employees with properties like Name, Email, Age, and Department
- **Company**: Represents companies with Name, Industry, and Founded date
- **WorksFor**: Relationship connecting Person to Company with Position, StartDate, and Salary

## Key Concepts Demonstrated

### 1. Node and Relationship Attributes

```csharp
[Node("Person")]
public record Person : Node { ... }

[Relationship("WORKS_FOR")]
public record WorksFor : Relationship<Person, Company> { ... }
```

### 2. Creating Graph Data

```csharp
await graph.CreateAsync(person);
await graph.CreateAsync(relationship);
```

### 3. LINQ Querying

```csharp
var people = await graph.NodesAsync<Person>()
    .Where(p => p.Department == "Engineering")
    .ToListAsync();
```

### 4. Updating Data

```csharp
var updatedPerson = person with { Age = 31 };
await graph.UpdateAsync(updatedPerson);
```

### 5. Deleting Data

```csharp
await graph.DeleteAsync(person);
```

## Prerequisites

- Neo4j instance running on `localhost:7687`
- Username: `neo4j`
- Password: `password`

## Running the Example

```bash
cd examples/Example1.BasicCRUD
dotnet run
```

## Expected Output

The example will:

1. Create company and employee nodes
2. Create work relationships
3. Query and display the data
4. Update some properties
5. Demonstrate deletion
6. Show verification of all operations

This example provides a foundation for understanding how to work with graph data using the GraphModel library.

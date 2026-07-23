# Example 1: Basic CRUD operations

This example demonstrates create, query, set-based update, and delete operations with the Neo4j
provider.

## Domain model

```csharp
[Node(Label = "Person")]
public record Person : Node
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Department { get; set; }
}

[Relationship(Label = "WORKS_FOR")]
public record WorksFor : Relationship
{
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}
```

Relationships do not store endpoint IDs. The creation command receives two query selections:

```csharp
await graph.CreateRelationshipAsync(
    graph.Nodes<Person>().Where(person => person.Email == alice.Email),
    new WorksFor { Position = "Senior Developer", Salary = 95_000m },
    graph.Nodes<Company>().Where(company => company.Name == techCorp.Name));
```

Queries begin synchronously and perform I/O at an async terminal:

```csharp
var engineers = await graph.Nodes<Person>()
    .Where(person => person.Department == "Engineering")
    .ToListAsync();
```

Updates and deletes operate on the selected set:

```csharp
await graph.Nodes<Person>()
    .Where(person => person.Email == alice.Email)
    .UpdateAsync(setters => setters
        .SetProperty(person => person.Age, 31)
        .SetProperty(person => person.Department, "Engineering"));

await graph.Nodes<Person>()
    .Where(person => person.Email == "temporary@example.com")
    .DeleteAsync();
```

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example1.BasicCRUD
```

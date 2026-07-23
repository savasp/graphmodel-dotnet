# Example 0: Basic serialization

This example exercises the Neo4j provider's entity serialization with scalar, nullable,
collection, complex, and dynamic values.

## What it demonstrates

- `Node` and `Relationship` records without public provider identity
- Scalar and enum collections
- Nullable complex properties and collections of complex values
- Dynamic nodes and relationships
- Relationship creation through selected endpoints

The model includes a simple `Person`, a `PersonWithComplex` with nested `Address`, `City`, `Foo`,
`Bar`, and `Baz` values, and a `Friend` relationship:

```csharp
[Node(Label = "Person")]
public record Person : Node
{
    public string Email { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
}

[Node(Label = "PersonWithAddress")]
public record PersonWithComplex : Node
{
    public Address HomeAddress { get; set; } = new();
    public Address? WorkAddress { get; set; }
    public List<Address> PreviousAddresses { get; set; } = [];
}

[Relationship(Label = "FRIEND_OF")]
public record Friend : Relationship
{
    public DateTime Since { get; set; }
}
```

Nodes are created directly. Existing relationship endpoints are expressed as exact-one query
selections:

```csharp
await graph.CreateNodeAsync(alice);

await graph.CreateRelationshipAsync(
    graph.Nodes<Person>().Where(person => person.Email == alice.Email),
    new Friend { Since = DateTime.UtcNow },
    graph.Nodes<Person>().Where(person => person.Email == bob.Email));
```

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example0.BasicSerialization
```

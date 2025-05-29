# Getting Started with Graph Model

This guide will walk you through the basics of using Graph Model in your .NET applications, covering installation, domain modeling, and basic operations.

## Installation

Add the Graph Model package to your project:

```bash
dotnet add package Cvoya.Graph.Model
```

You'll also need a graph provider implementation, such as:

```bash
dotnet add package Cvoya.Graph.Provider.Neo4j
```

## Basic Concepts

### 1. Define Your Domain Model

Start by defining your nodes and relationships with proper attributes and modern C# features:

```csharp
using Cvoya.Graph.Model;

[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("first_name", Index = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name", Index = true)]
    public string LastName { get; set; } = string.Empty;

    [Property(Index = true)]
    public int Age { get; set; }

    [Property]
    public string Bio { get; set; } = string.Empty;

    [Property("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Property(Ignore = true)]
    public string FullName => $"{FirstName} {LastName}";
}

[Relationship("KNOWS", Direction = RelationshipDirection.Bidirectional)]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; } = true;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property("since_date", Index = true)]
    public DateTime Since { get; set; }

    [Property]
    public string Relationship { get; set; } = "friend";
}
```

### 2. Create a Graph Instance

The specific implementation depends on your chosen provider:

```csharp
// Example with Neo4j provider
var graph = new Neo4jGraphProvider(driver);
```

### 3. Basic Operations

#### Creating Nodes

```csharp
var alice = new Person
{
    FirstName = "Alice",
    LastName = "Smith",
    Age = 30,
    Bio = "Software engineer passionate about graphs"
};

await graph.CreateNode(alice);
// The Id property is automatically populated after creation
```

#### Creating Relationships

```csharp
var bob = new Person { FirstName = "Bob", LastName = "Jones", Age = 25 };
await graph.CreateNode(bob);

var knows = new Knows
{
    Source = alice,
    Target = bob,
    Since = DateTime.UtcNow
};

await graph.CreateRelationship(knows);
```

#### Querying Nodes

```csharp
// Get a single node by ID
var person = await graph.GetNode<Person>(alice.Id);

// Query with LINQ
var smiths = graph.Nodes<Person>()
    .Where(p => p.LastName == "Smith")
    .OrderBy(p => p.FirstName)
    .ToList();

// Complex queries
var youngEngineers = graph.Nodes<Person>()
    .Where(p => p.Age < 35 && p.Bio.Contains("engineer"))
    .Select(p => new { p.FirstName, p.Age })
    .ToList();
```

#### Updating Entities

```csharp
alice.Age = 31;
await graph.UpdateNode(alice);

knows.Since = DateTime.UtcNow.AddDays(-365);
await graph.UpdateRelationship(knows);
```

#### Deleting Entities

```csharp
await graph.DeleteRelationship(knows.Id);
await graph.DeleteNode(alice.Id);
```

## Working with Transactions

For operations that need to be atomic:

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    await graph.CreateNode(newPerson, transaction: transaction);
    await graph.CreateRelationship(newRelationship, transaction: transaction);

    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

## Loading Related Data

Control how deep relationships are loaded:

```csharp
// Load nodes with their immediate relationships
var options = new GraphOperationOptions { TraversalDepth = 1 };
var peopleWithFriends = graph.Nodes<Person>(options)
    .Where(p => p.FirstName == "Alice")
    .ToList();

// Access loaded relationships
foreach (var person in peopleWithFriends)
{
    Console.WriteLine($"{person.FirstName} knows {person.Knows.Count} people");
}
```

## Next Steps

- Learn about [advanced querying techniques](querying.md)
- Understand [transaction management](transactions.md)
- Explore [attribute configuration](attributes.md)
- Read [best practices](best-practices.md)

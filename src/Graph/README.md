**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph

[![NuGet](https://img.shields.io/nuget/v/Cvoya.Graph.svg)](https://www.nuget.org/packages/Cvoya.Graph/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

`Cvoya.Graph` is the provider-neutral model, LINQ query surface, transaction contract, and mutation
API for CVOYA Graph.

## Entity model

`IEntity` is an empty marker. `INode` exposes labels and `IRelationship` exposes its type; neither
interface exposes provider identity. Relationship endpoints and physical direction are returned by
path segments rather than stored on relationship values.

Domain keys are optional:

```csharp
[Node(Label = "User")]
public record User : Node
{
    [Property(IsKey = true)]
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

[Relationship(Label = "FOLLOWS")]
public record Follows : Relationship
{
    public DateTime Since { get; set; }
}
```

Remove `IsKey` and `User` remains a valid keyless entity. A property named `Id` or `Direction` is
ordinary domain data unless its attributes say otherwise.

## Query and mutation

Install a database provider, obtain its `IGraph`, and compose synchronous query roots with async
terminals:

```csharp
var activeUsers = await graph.Nodes<User>()
    .Where(user => user.IsActive)
    .OrderBy(user => user.Name)
    .ToListAsync(cancellationToken);

var updated = await graph.Nodes<User>()
    .Where(user => user.Email.EndsWith("@old.example"))
    .UpdateAsync(setters => setters
        .SetProperty(user => user.IsActive, false),
        cancellationToken);
```

Create relationships by supplying endpoint intent separately from relationship data:

```csharp
var alice = graph.Nodes<User>().Where(user => user.Email == "alice@example.com");
var bob = graph.Nodes<User>().Where(user => user.Email == "bob@example.com");

await graph.CreateRelationshipAsync(
    alice,
    new Follows { Since = DateTime.UtcNow },
    bob,
    cancellationToken: cancellationToken);
```

Each selected endpoint must resolve to exactly one node. Hybrid and all-new overloads are also
available through `CreateAsync`; `CreateSelfLoopAsync` handles one new node used as both endpoints.

## Core contracts

- `IGraph` — query roots, search, transactions, standalone node creation, and managed-index refresh
- `IGraphQueryable<T>` — typed LINQ composition, traversal, and async terminals
- `INode`, `IRelationship`, `IGraphPath`, and `IGraphPathSegment` — portable graph values
- `GraphCommandExtensions` — endpoint-intent creation plus set-based update and delete
- `[Node]`, `[Relationship]`, and `[Property]` — mapping and optional schema metadata

## Related packages

- [Cvoya.Graph.Neo4j](https://www.nuget.org/packages/Cvoya.Graph.Neo4j/) — Neo4j provider
- [Cvoya.Graph.Age](https://www.nuget.org/packages/Cvoya.Graph.Age/) — PostgreSQL + Apache AGE provider
- [Cvoya.Graph.InMemory](https://www.nuget.org/packages/Cvoya.Graph.InMemory/) — reference provider and test double
- [Cvoya.Graph.Cypher](https://www.nuget.org/packages/Cvoya.Graph.Cypher/) — Cypher planner, AST, renderer, and dialect SPI
- [Cvoya.Graph.Serialization](https://www.nuget.org/packages/Cvoya.Graph.Serialization/) — provider-neutral serialization and materialization
- [Cvoya.Graph.Serialization.CodeGen](https://www.nuget.org/packages/Cvoya.Graph.Serialization.CodeGen/) — incremental serializer generator
- [Cvoya.Graph.Analyzers](https://www.nuget.org/packages/Cvoya.Graph.Analyzers/) — optional model diagnostics
- [Cvoya.Graph.CompatibilityTests](https://www.nuget.org/packages/Cvoya.Graph.CompatibilityTests/) — provider contract suite

See the [complete documentation](https://github.com/cvoya-com/graph/), the
[contributing guide](https://github.com/cvoya-com/graph/blob/main/CONTRIBUTING.md), and the
[Apache 2.0 license](https://github.com/cvoya-com/graph/blob/main/LICENSE).

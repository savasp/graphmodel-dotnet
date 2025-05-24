# Core Interfaces

Graph Model provides a set of core interfaces that define the contract for working with graph data structures.

## IGraph

The main entry point for interacting with a graph. Provides CRUD operations, querying, and transaction management.

```csharp
public interface IGraph : IDisposable
{
    // Query operations
    IQueryable<N> Nodes<N>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : INode, new();

    IQueryable<R> Relationships<R>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : IRelationship, new();

    // CRUD operations
    Task<T> GetNode<T>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    Task CreateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    Task UpdateNode<T>(T node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where T : INode, new();

    Task DeleteNode(string id, IGraphTransaction? transaction = null);

    // Transaction management
    Task<IGraphTransaction> BeginTransaction();
}
```

## IEntity

Base interface for all graph entities (nodes and relationships).

```csharp
public interface IEntity
{
    string Id { get; set; }
}
```

### Key Points:

- Every entity in the graph must have a unique identifier
- The Id is typically assigned by the graph database when the entity is created
- The Id should be treated as immutable after creation

## INode

Represents a node (vertex) in the graph.

```csharp
public interface INode : IEntity
{
}
```

### Implementation Guidelines:

- Implement `INode` directly on your domain classes
- Use the `[Node]` attribute to specify the node label
- Properties become node properties in the graph
- Navigation properties (collections of relationships) are supported

### Example:

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; }

    [Property("full_name")]
    public string FullName { get; set; }

    public int Age { get; set; }

    // Navigation property
    public IList<Knows> Friends { get; set; }
}
```

## IRelationship

Represents a directional relationship (edge) between nodes.

```csharp
public interface IRelationship : IEntity
{
    bool IsBidirectional { get; set; }
    string SourceId { get; set; }
    string TargetId { get; set; }
}
```

### Key Properties:

- `SourceId` - The ID of the source node
- `TargetId` - The ID of the target node
- `IsBidirectional` - Whether the relationship should be treated as bidirectional

## IRelationship<TSource, TTarget>

A typed relationship that provides strong typing for source and target nodes.

```csharp
public interface IRelationship<S, T> : IRelationship
    where S : INode
    where T : INode
{
    S? Source { get; set; }
    T? Target { get; set; }
}
```

### Benefits:

- Type safety when working with relationships
- Navigation properties for easy traversal
- IntelliSense support in IDEs

### Example:

```csharp
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    public DateTime Since { get; set; }
}
```

## IGraphTransaction

Represents a graph database transaction.

```csharp
public interface IGraphTransaction : IAsyncDisposable
{
    Task Commit();
    Task Rollback();
}
```

### Usage Pattern:

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Perform operations
    await graph.CreateNode(node, transaction: transaction);

    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

## GraphOperationOptions

Controls the behavior of graph operations.

```csharp
public struct GraphOperationOptions
{
    public int TraversalDepth { get; set; }
    // Additional options...
}
```

### Key Options:

- `TraversalDepth` - How many levels of relationships to load (0 = no relationships, -1 = unlimited)

## Base Classes

Graph Model provides abstract base classes for convenience:

### Node

```csharp
public abstract class Node : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
}
```

### Relationship

```csharp
public abstract class Relationship : IRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}
```

### Relationship<TSource, TTarget>

```csharp
public class Relationship<S, T> : IRelationship<S, T>
    where S : class, INode
    where T : class, INode
{
    // Implementation...
}
```

These base classes provide default implementations and can be extended for your domain models.

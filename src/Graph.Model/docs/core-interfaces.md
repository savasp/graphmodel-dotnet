# Core Interfaces

Graph Model provides a comprehensive set of core interfaces that define the contract for working with graph data structures, enhanced with modern C# features and advanced querying capabilities.

## IGraph

The main entry point for interacting with a graph. Provides CRUD operations, enhanced querying capabilities, and transaction management with full async support.

```csharp
public interface IGraph : IAsyncDisposable
{
    // Enhanced query operations returning IGraphQueryable
    IGraphQueryable<N> Nodes<N>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, INode, new();

    IGraphQueryable<R> Relationships<R>(GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, IRelationship, new();

    // CRUD operations with enhanced error handling
    Task<N> GetNode<N>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, INode, new();

    Task<IEnumerable<N>> GetNodes<N>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, INode, new();

    Task<R> GetRelationship<R>(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, IRelationship, new();

    Task<IEnumerable<R>> GetRelationships<R>(IEnumerable<string> ids, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, IRelationship, new();

    Task CreateNode<N>(N node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, INode, new();

    Task CreateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, IRelationship, new();

    Task UpdateNode<N>(N node, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where N : class, INode, new();

    Task UpdateRelationship<R>(R relationship, GraphOperationOptions options = default, IGraphTransaction? transaction = null)
        where R : class, IRelationship, new();

    Task DeleteNode(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null);

    Task DeleteRelationship(string id, GraphOperationOptions options = default, IGraphTransaction? transaction = null);

    // Transaction management
    Task<IGraphTransaction> BeginTransaction();
}
```

### Key Features:

- **Enhanced querying** - Returns `IGraphQueryable<T>` instead of basic `IQueryable<T>` for graph-specific operations
- **Comprehensive CRUD** - Support for single and bulk operations
- **Flexible transactions** - Optional transaction parameter on all operations
- **Consistent error handling** - `GraphException` and `GraphTransactionException` for predictable error management

## IEntity

Base interface for all graph entities (nodes and relationships).

```csharp
public interface IEntity
{
    string Id { get; set; }
}
```

### Key Points

- Every entity in the graph must have a unique identifier
- The Id is typically assigned by the graph database when the entity is created
- The Id should be treated as immutable after creation
- Supports both nodes and relationships through inheritance

## INode

Represents a node (vertex) in the graph.

```csharp
public interface INode : IEntity
{
}
```

### Implementation Guidelines

- Implement `INode` directly on your domain classes
- Use the `[Node]` attribute to specify the node label
- Use `[Property]` attributes for custom property mapping and indexing
- Properties become node properties in the graph
- Navigation properties (collections of relationships) are supported

### Example

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Property("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Property(Index = true)]
    public int Age { get; set; }

    [Property(Ignore = true)]
    public string TempCalculation { get; set; } = string.Empty;

    // Navigation properties can be added for relationships
    // These are typically populated during traversal operations
}
```

## IRelationship

Represents a directional relationship (edge) between nodes.

```csharp
public interface IRelationship : IEntity
{
    bool IsBidirectional { get; set; }
    string StartNodeId { get; set; }
    string EndNodeId { get; set; }
}
```

### Key Properties

- `StartNodeId` - The ID of the source node
- `EndNodeId` - The ID of the target node
- `IsBidirectional` - Whether the relationship should be treated as bidirectional

### Usage Notes

- Always set `StartNodeId` and `EndNodeId` when creating relationships
- The `IsBidirectional` property is a hint to graph providers about traversal behavior
- Use `[Relationship]` attribute to specify custom labels and direction

## IRelationship<TSource, TTarget>

A strongly-typed relationship that provides type safety for source and target nodes.

```csharp
public interface IRelationship<S, T> : IRelationship
    where S : INode
    where T : INode
{
    S? Source { get; set; }
    T? Target { get; set; }
}
```

### Benefits

- Type safety when working with relationships
- Navigation properties for easy traversal
- IntelliSense support in IDEs
- Automatic ID synchronization between object references and ID properties

### Example

```csharp
[Relationship("KNOWS", Direction = RelationshipDirection.Bidirectional)]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; } = true;

    public Person? Source { get; set; }
    public Person? Target { get; set; }

    public DateTime Since { get; set; }
    public string Relationship { get; set; } = string.Empty;
}
```

## IGraphTransaction

Represents a graph database transaction with full async support.

```csharp
public interface IGraphTransaction : IAsyncDisposable, IDisposable
{
    Task Commit();
    Task Rollback();
}
```

### Usage Pattern

```csharp
await using var transaction = await graph.BeginTransaction();
try
{
    // Perform operations
    await graph.CreateNode(person, transaction: transaction);
    await graph.CreateNode(address, transaction: transaction);
    var livesAt = new LivesAt { StartNodeId = person.Id, EndNodeId = address.Id };
    await graph.CreateRelationship(livesAt, transaction: transaction);

    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}
```

### Key Features

- Implements both `IAsyncDisposable` and `IDisposable`
- Automatic rollback on disposal if not explicitly committed
- Full async/await support
- Throws `GraphTransactionException` on transaction errors

## GraphOperationOptions

Controls the behavior of graph operations.

```csharp
public struct GraphOperationOptions
{
    public bool CascadeDelete { get; set; } = false;
}
```

### Key Options

- `CascadeDelete` - Whether to delete related nodes when deleting relationships

### Usage

```csharp
var options = new GraphOperationOptions { CascadeDelete = true };
await graph.DeleteRelationship("rel-123", options);
```

## IGraphQueryable<T>

Enhanced queryable interface that extends `IQueryable<T>` with graph-specific functionality.

```csharp
public interface IGraphQueryable<T> : IQueryable<T> where T : class
{
    GraphOperationOptions Options { get; }
    IGraphTransaction? Transaction { get; }
    IGraphQueryContext Context { get; }

    IGraphQueryable<T> WithOptions(GraphOperationOptions options);
    IGraphQueryable<T> WithDepth(int depth);
    IGraphQueryable<T> WithDepth(int minDepth, int maxDepth);
    IGraphQueryable<T> InTransaction(IGraphTransaction transaction);
    IGraphQueryable<T> WithHint(string hint);
    IGraphQueryable<T> UseIndex(string indexName);
    IGraphQueryable<T> Cached(TimeSpan duration);
    IGraphQueryable<T> WithTimeout(TimeSpan timeout);
    IGraphQueryable<T> WithProfiling();

    // Graph-specific operations
    IGraphTraversal<TSource, TRel> Traverse<TSource, TRel>()
        where TSource : class, INode, new()
        where TRel : class, IRelationship, new();
}
```

### Key Features

- **Depth control** - Specify traversal depth for relationship loading
- **Performance optimization** - Query caching, hints, and profiling
- **Transaction support** - Query within specific transaction contexts
- **Graph traversal** - Initiate complex traversal operations
- **Full LINQ compatibility** - All standard LINQ operations work seamlessly

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
    public string StartNodeId { get; set; } = string.Empty;
    public string EndNodeId { get; set; } = string.Empty;
    public bool IsBidirectional { get; set; }
}
```

These base classes provide default implementations and can be extended for your domain models, offering sensible defaults for common scenarios.

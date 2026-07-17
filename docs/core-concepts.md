---
---

# Core Interfaces and Type System

This document provides a comprehensive guide to the core interfaces and type system that form the foundation of Graph Model. Understanding these interfaces is essential for effective use of the library.

## 📦 Getting Started: Required Packages

To use Graph Model in your project, you only need to install the Neo4j provider package:

```bash
# Required
dotnet add package Cvoya.Graph.Neo4j

# Optional (recommended for extra compile-time validation)
dotnet add package Cvoya.Graph.Analyzers
```

The Neo4j provider package will automatically bring in all required dependencies and enable code generation. The analyzers package is optional but recommended for additional compile-time checks.

## 🏗️ Architecture Overview

Graph Model follows a layered architecture with clean abstractions:

```text
┌─────────────────────────┐
│    Your Domain Model    │  ← Your nodes and relationships
├─────────────────────────┤
│     Core Interfaces     │  ← INode, IRelationship, IGraph
├─────────────────────────┤
│    Provider Layer       │  ← Neo4j, Future providers
├─────────────────────────┤
│    Database Layer       │  ← Neo4j, etc.
└─────────────────────────┘
```

## 🔧 Foundation: IEntity

All graph entities inherit from `IEntity`, which provides the fundamental identity contract:

```csharp
/// <summary>
/// Represents the foundation for all entities in the graph model.
/// This is the base interface for both nodes and relationships, providing core identity functionality.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// Identifiers should be immutable once the entity has been persisted to ensure referential integrity.
    /// </summary>
    string Id { get; init; }
}
```

### Key Points

- **Immutable after persistence**: Once saved to the graph, the `Id` should not change
- **String-based**: Uses strings for maximum flexibility across different graph databases
- **Required for all entities**: Both nodes and relationships must have unique identifiers

### Example Implementation

```csharp
public class Person : INode
{
    // CG011 warns on direct INode implementations; inherit from Node unless you need full control.
    // Good: Using init-only setter
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public IReadOnlyList<string> Labels { get; } = Array.Empty<string>();

    // Alternative: inherit from Node to get Id and Labels automatically.
}
```

## 🎯 Nodes: INode Interface

Nodes represent the primary entities in your graph model:

```csharp
/// <summary>
/// Defines the contract for node entities in the graph model.
/// Nodes represent primary data entities that can be connected via relationships.
/// </summary>
public interface INode : IEntity
{
    /// <summary>
    /// Gets the labels for this node as they are stored in the graph database.
    /// This is a runtime property that reflects the actual labels assigned to the node.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the graph provider when the node is
    /// retrieved from or saved to the database. The labels are derived from the NodeAttribute
    /// on the implementing type, or the type name if no attribute is present.
    ///
    /// This property enables polymorphic queries and filtering by label at runtime,
    /// complementing the compile-time type system.
    ///
    /// Do not set this property manually - it is managed by the graph provider.
    /// </remarks>
    IReadOnlyList<string> Labels { get; }
}
```

### Design Philosophy

- **Runtime Metadata**: `INode.Labels` provides runtime access to node labels for polymorphic queries
- **Provider-Managed**: The Labels property is automatically populated by the graph provider
- **Domain-Focused**: Your node classes represent your actual business entities
- **Base Class Recommended**: Use the `Node` base class which provides default implementations

### Node Implementation Patterns

> **Best Practice**: Always inherit from the `Node` base class instead of implementing `INode` directly. The base class provides automatic ID generation and proper runtime metadata management.

#### 1. Simple Node (Recommended)

```csharp
[Node("Person")]
public record Person : Node  // Use Node base class
{
    [Property(Label = "first_name", IsIndexed = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property(Label = "last_name", IsIndexed = true)]
    public string LastName { get; set; } = string.Empty;

    // The Property attribute is optional
    public int Age { get; set; }

    // Computed property - excluded from persistence
    [Property(Ignore = true)]
    public string FullName => $"{FirstName} {LastName}";
}
```

#### 2. Node with Complex Properties

```csharp
[Node("Company")]
public record Company : Node  // Use Node base class
{
    public string Name { get; set; } = string.Empty;

    [ComplexProperty(RelationshipType = "HEADQUARTERED_AT")]
    public Address Headquarters { get; set; } = new(); // Complex type
    public List<Address> Offices { get; set; } = new(); // Collection
}

public class Address // A value object in the CLR model
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```

Complex CLR properties are stored as first-class value nodes and semantic relationships. In this
example the graph contains `(:Company)-[:HEADQUARTERED_AT]->(:Address)` and one `:Offices` edge per
collection item. The attribute is optional; without it, the property name is the relationship type.
Each occurrence gets its own value node, preserving value-object rather than shared-entity semantics.
Declared properties round-trip transparently up to five nested levels; deeper graphs and cycles are
rejected.

Concurrent updates of the same owner in separate transactions serialize on the owner node's write
lock: the final state is one writer's complete value subtree (last committed writer wins; under
contention the provider may abort one transaction instead) — never an interleaved mix or orphaned
value nodes.

Because complex-property edges are ordinary edges, a user-declared domain relationship may legally
share its type with a complex-property relationship type (for example a `PRIMARY_ADDRESS` domain
relationship alongside a `[ComplexProperty(RelationshipType = "PRIMARY_ADDRESS")]` property).
Provider-internal marker metadata on property edges keeps the two apart where it matters: update
and delete cleanup removes only property value nodes (domain relationships and their targets are
never deleted by property cleanup), and loading an entity materializes only genuine property
values. LINQ predicates that navigate a complex property (such as
`Where(o => o.Address.City == "…")`) match on relationship type and target label alone, so a
colliding domain relationship whose target has the right shape also satisfies them. To avoid that
surprise, prefer distinct relationship type names for domain relationships and complex properties.

##### Null semantics of complex-property navigation

Navigating a complex property inside a query (`p.Address.City` in a `Where`, `Select`, or
`OrderBy`) is **null-propagating**, like C#'s `?.` operator and Cypher's own null semantics:

- A predicate branch that navigates a missing complex property is simply not satisfied — it does
  not remove the row from the query. `Where(p => p.Address.City == "X" || p.Name == "Y")` still
  matches people without an `Address` when the `Name` branch holds.
- Projecting or ordering through a missing complex property yields `null` for that value rather
  than dropping the row.
- A leaf null-comparison through a navigation treats "the complex property is absent" and "the
  leaf value is null" alike: `p.Address.City == null` matches people with no `Address` node **and**
  people whose `Address.City` is stored as null. To distinguish the two, test the complex property
  itself — `p.Address == null` is true exactly when no `Address` node exists.

This decision is recorded on issue #221; a future revisit of the leaf-null conflation is tracked
in issue #233.

#### 3. Node Inheritance Hierarchy

Implementations of the Graph Model must support polymorphic behavior.

```csharp
[Node("Person")]
public abstract record Person : Node  // Use Node base class
{

    [Property(Label = "first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Property(Label = "last_name")]
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
}

[Node("Employee")]
public record Employee : Person
{
    [Property(Label = "employee_id", IsIndexed = true)]
    public string EmployeeId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}

[Node("Manager")]
public record Manager : Employee
{
    public string ManagementLevel { get; set; } = string.Empty;
}
```

If you add a manager to the graph via an employee variable, you will get back a manager even if you ask for an Employee...

```csharp
Employee employee = new Manager
{
    FirstName = "Alice",
    LastName = "Smith",
    EmployeeId = "E-001",
    Department = "Engineering",
    ManagementLevel = "Director"
};
await graph.CreateNodeAsync(employee);

Person person = await graph.GetNodeAsync<Employee>(employee.Id);
Assert.Equal(typeof(Manager), person.GetType());
```

## 🔗 Relationships: IRelationship Interfaces

Relationships connect nodes and form the graph structure. There are two main relationship interfaces:

### Basic Relationships: IRelationship

```csharp
/// <summary>
/// Defines the contract for relationship entities in the graph model.
/// Relationships connect two nodes and can have their own properties.
/// </summary>
public interface IRelationship : IEntity
{
    /// <summary>
    /// Gets the type of this relationship as it is stored in the graph database.
    /// This is a runtime property that reflects the actual relationship type.
    /// </summary>
    /// <remarks>
    /// This property is automatically populated by the graph provider when the relationship is
    /// retrieved from or saved to the database. The type is derived from the RelationshipAttribute
    /// on the implementing type, or the type name if no attribute is present.
    ///
    /// This property enables polymorphic queries and filtering by type at runtime,
    /// complementing the compile-time type system.
    ///
    /// Do not set this property manually - it is managed by the graph provider.
    /// </remarks>
    string Type { get; }

    /// <summary>
    /// Gets the physical storage direction of this relationship.
    /// Outgoing means the stored edge points from StartNodeId to EndNodeId.
    /// Incoming means the stored edge points from EndNodeId to StartNodeId.
    /// </summary>
    RelationshipDirection Direction { get; init; }

    /// <summary>
    /// Gets the ID of the start node in this relationship.
    /// This is the ID of the node from which the relationship originates.
    /// </summary>
    string StartNodeId { get; init; }

    /// <summary>
    /// Gets the ID of the end node in this relationship.
    /// This is the ID of the node to which the relationship points.
    /// </summary>
    string EndNodeId { get; init; }
}
```

> **Best Practice**: Always inherit from the `Relationship` base class instead of implementing `IRelationship` directly. The base class provides automatic ID generation and proper runtime metadata management.

### Strongly-Typed Relationships: IRelationship<TSource, TTarget>

```csharp
/// <summary>
/// Defines a strongly-typed relationship between two specific node types.
/// This interface extends IRelationship by adding strongly-typed references
/// to the actual source and target node objects, facilitating more type-safe graph traversal.
/// </summary>
/// <typeparam name="TSource">The type of the source node in the relationship.</typeparam>
/// <typeparam name="TTarget">The type of the target node in the relationship.</typeparam>
public interface IRelationship<TSource, TTarget> : IRelationship
    where TSource : class, INode, new()
    where TTarget : class, INode, new()
{
    /// <summary>
    /// Gets or sets the source node of the relationship.
    /// When set, this also updates the StartNodeId property.
    /// May be null if the relationship is not fully loaded.
    /// </summary>
    TSource Source { get; set; }

    /// <summary>
    /// Gets or sets the target node of the relationship.
    /// When set, this also updates the EndNodeId property.
    /// May be null if the relationship is not fully loaded.
    /// </summary>
    TTarget Target { get; set; }
}
```

### Relationship Direction

The `RelationshipDirection` enum describes storage direction, not query traversal. Graph databases
store a physical arrow for each relationship. `StartNodeId` and `EndNodeId` name the logical node
tuple on your relationship object, and `Direction` says which way the stored arrow points relative
to that tuple:

```csharp
public enum RelationshipDirection
{
    Outgoing, // Stored as StartNodeId -> EndNodeId
    Incoming  // Stored as EndNodeId -> StartNodeId
}
```

Traversal direction is a query-time choice. Use `GraphTraversalDirection.Outgoing`,
`GraphTraversalDirection.Incoming`, or `GraphTraversalDirection.Both` on traversal operators when
you want to choose which stored arrows a query follows.

Relationship identity is immutable once persisted. Updating properties succeeds only when the
incoming relationship has the same storage type, concrete CLR/materialization type, and `Direction`.
Changing any of those identity fields during an update throws `GraphException` before properties are
mutated. Delete and recreate the relationship when you need a different type or stored direction;
providers do not perform that operation implicitly because recreation can affect endpoints, schemas,
and constraints.

### Relationship Implementation Patterns

#### 1. Simple Relationship (Recommended)

```csharp
[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
    public string KnownAs { get; set; } = "acquaintance";  // Custom property
}
```

#### 2. Strongly-Typed Relationship (Not Yet Supported)

> **Note**: Strongly-typed relationships (`IRelationship<TSource, TTarget>`) are currently being redesigned and are not yet supported in the current version. Use the simple relationship pattern shown above.

```text
Pseudo-code: coming in a future release.
[Relationship("KNOWS")]
public record Knows : Relationship<Person, Person>
{
    public DateTime Since { get; set; }
    public int Strength { get; set; } = 1; // 1-10 scale
}
```

#### 3. Business-Specific Relationships

```csharp
[Relationship("EMPLOYS")]
public record Employment(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public string Position { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Salary { get; set; }
    public EmploymentType EmploymentKind { get; set; } = EmploymentType.FullTime;

    // Computed property
    [Property(Ignore = true)]
    public bool IsActive => EndDate == null;

    [Property(Ignore = true)]
    public TimeSpan Duration => (EndDate ?? DateTime.UtcNow) - StartDate;
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Intern
}
```

### Creating a subgraph atomically

`CreateAsync(source, relationship, target, options)` creates a node–relationship–node subgraph —
both endpoint nodes and the relationship that connects them — as a single atomic operation. The
Neo4j provider composes one Cypher statement; the Apache AGE provider sends one Npgsql batch whose
commands share the operation transaction. In both cases the whole subgraph (both nodes, all of their
complex-property value-node subtrees, and the edge) is created in a single database round-trip.

```csharp
var alice = new Person { FirstName = "Alice" };
var bob = new Person { FirstName = "Bob" };
var knows = new Knows { StartNodeId = alice.Id, EndNodeId = bob.Id, Since = DateTime.UtcNow };

await graph.CreateAsync(alice, knows, bob);
```

Semantics:

- **Atomic.** The whole subgraph is created as one transactional unit. If any part fails, nothing
  is created.
- **Endpoint ids must match.** `relationship.StartNodeId` and `relationship.EndNodeId` must equal
  `source.Id` and `target.Id` respectively; otherwise an `ArgumentException` is thrown before any
  work is done.
- **Direction is honored.** The relationship's `Direction` determines the stored edge direction,
  exactly as for `CreateRelationshipAsync`.
- **Default (`CreateMissingEndpoints = false`).** Both endpoint nodes are *created*. If a node with
  an endpoint id already exists, the operation fails atomically and creates nothing — matching the
  create-only semantics of `CreateNodeAsync`.
- **`CreateMissingEndpoints = true`.** Each endpoint is *merged* by id. An endpoint that already
  exists is reused **entirely as-is** — only its id is used to match, and both its simple properties
  and its existing complex-property subtrees are left untouched (the properties you pass on that
  endpoint object are ignored). A missing endpoint is created with its full properties **and** its
  complex-property subtree. The edge is always created. All providers (Neo4j, Apache AGE, in-memory)
  agree on this.

```csharp
// Reuse an existing "alice" node if present, create "bob" and the edge:
await graph.CreateAsync(
    alice, knows, bob,
    new GraphOperationOptions { CreateMissingEndpoints = true });
```

## 🎨 Attribute Configuration

Graph Model uses attributes to configure how entities are mapped to the graph database:

### Node Attribute

```csharp
/// <summary>
/// Attribute to specify custom labels for graph nodes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public class NodeAttribute : Attribute
{
    public string Label { get; set; }
}
```

Examples:

```csharp
[Node("Person")]
public record Manager : Node { }
```

### Relationship Attribute

```csharp
/// <summary>
/// Attribute to customize aspects of a relationship in the graph.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public class RelationshipAttribute : Attribute
{
    public RelationshipAttribute(string label) { }

    public string? Label { get; set; }
}
```

Examples:

```csharp
[Relationship("KNOWS")]
public record ReportsTo(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId);
```

### Property Attribute

```csharp
/// <summary>
/// Attribute to control property mapping and behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class PropertyAttribute : Attribute
{
    public PropertyAttribute() { }

    public string? Label { get; set; }     // Custom property name
    public bool Ignore { get; set; }       // Exclude from persistence
}
```

Examples:

```csharp
public record Person : Node
{
    [Property(Label = "first_name")]        // Custom name
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    [Property]                              // Auto name (uses property name)
    public string Email { get; set; } = string.Empty;

    [Property(Ignore = true)]               // Excluded from persistence
    public string DisplayName => $"{FirstName} {LastName}";

    public int Age { get; set; }            // Default mapping (no attribute needed)
}
```

## 🚀 Graph Interface: IGraph

The `IGraph` interface is the main entry point for all graph operations:

```csharp
/// <summary>
/// Interface for the Graph client. Provides CRUD operations for nodes and relationships,
/// querying, and transaction management.
/// All methods throw GraphException for underlying graph errors.
/// </summary>
public interface IGraph
{
    // Synchronous query roots for LINQ support - building a queryable performs no I/O; any
    // transaction/session acquisition happens when the query is executed.
    IGraphQueryable<TNode> Nodes<TNode>(IGraphTransaction? transaction = null) where TNode : class, INode;
    IGraphQueryable<TRelationship> Relationships<TRelationship>(IGraphTransaction? transaction = null) where TRelationship : class, IRelationship;

    // CRUD operations
    Task<TNode> GetNodeAsync<TNode>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TNode : class, INode;
    Task<TRelationship> GetRelationshipAsync<TRelationship>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TRelationship : class, IRelationship;

    Task CreateNodeAsync<TNode>(TNode node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TNode : class, INode;
    Task CreateRelationshipAsync<TRelationship>(TRelationship relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TRelationship : class, IRelationship;

    // Atomic node–relationship–node subgraph create (both endpoints + edge in one operation)
    Task CreateAsync<TSource, TRelationship, TTarget>(TSource source, TRelationship relationship, TTarget target, GraphOperationOptions? options = null, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default)
        where TSource : class, INode where TRelationship : class, IRelationship where TTarget : class, INode;

    Task UpdateNodeAsync<TNode>(TNode node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TNode : class, INode;
    Task UpdateRelationshipAsync<TRelationship>(TRelationship relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where TRelationship : class, IRelationship;

    Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Transaction management
    Task<IGraphTransaction> GetTransactionAsync(CancellationToken cancellationToken = default);
}
```

## 📊 Query Interfaces

### IGraphQueryable&lt;T&gt;

The single foundation of LINQ support in Graph Model — node queries, relationship queries, and
projections are all `IGraphQueryable<T>`; graph-specific operators (e.g. traversal) are gated by
generic constraints on the operator (`where T : INode`), not by a separate receiver interface:

```csharp
/// <summary>
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends IQueryable&lt;T&gt; with additional functionality specific to graph operations.
/// </summary>
/// <typeparam name="T">The type of data being queried.</typeparam>
public interface IGraphQueryable<out T> : IQueryable<T>, IAsyncEnumerable<T>
{
    IGraph Graph { get; }
    new IGraphQueryProvider Provider { get; }
}
```

`IGraphQueryable<T>` is covariant (`out T`) — this is what lets the traversal operators below take
only the relationship and end-node types as explicit type arguments: any `IGraphQueryable<TStart>`
where `TStart : INode` converts to `IGraphQueryable<INode>` at the call site, so the start type
rides in on the receiver rather than a generic slot the caller could mismatch (`PathSegments` is the
one exception, since its result type `IGraphPathSegment<TStart,TRel,TEnd>` names the start type).

Depth and direction are configured on the traversal operators themselves (overloads or an options
lambda), not via free-floating postfix modifiers:

```csharp
graph.Nodes<Person>().Traverse<Knows, Person>(minDepth: 1, maxDepth: 3);
graph.Nodes<Person>().Traverse<Knows, Person>(o => o.Depth(1, 3).Direction(GraphTraversalDirection.Incoming));
```

## ⚡ Transaction Interface

```csharp
/// <summary>
/// Represents a graph transaction that supports ACID operations.
/// </summary>
public interface IGraphTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits all changes made in this transaction.
    /// </summary>
    Task CommitAsync();

    /// <summary>
    /// Rolls back all changes made in this transaction.
    /// </summary>
    Task RollbackAsync();
}
```

### Transaction Usage Patterns

```csharp
// Pattern 1: Explicit transaction management
await using var transaction = await graph.GetTransactionAsync();
try
{
    await graph.CreateNodeAsync(node, transaction: transaction);
    await graph.CreateRelationshipAsync(relationship, transaction: transaction);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// Pattern 2: Using statement with automatic rollback
await using var secondTransaction = await graph.GetTransactionAsync();
await graph.CreateNodeAsync(node, transaction: secondTransaction);
await graph.CreateRelationshipAsync(relationship, transaction: secondTransaction);
await secondTransaction.CommitAsync(); // Must explicitly commit
```

## 🔍 Path Segments

For advanced graph analysis, Graph Model provides path segment interfaces:

```csharp
/// <summary>
/// Represents a path segment in graph traversal, containing start node, relationship, and end node.
/// </summary>
public interface IGraphPathSegment<TStartNode, TRelationship, TEndNode>
    where TStartNode : INode
    where TRelationship : IRelationship
    where TEndNode : INode
{
    TStartNode StartNode { get; }
    TRelationship Relationship { get; }
    TEndNode EndNode { get; }
}
```

Usage example:

```csharp
// Analyze connection paths
var connectionAnalysis = await graph.Nodes<Person>()
    .Where(p => p.FirstName == "Alice")
    .PathSegments<Person, Knows, Person>()
    .Where(path => path.EndNode.Age > 25)
    .Select(path => new {
        From = path.StartNode.FullName,
        To = path.EndNode.FullName,
        Since = path.Relationship.Since,
        Strength = path.Relationship.Strength
    })
    .ToListAsync();
```

## 🔍 Full-Text Search

Graph Model provides comprehensive full-text search capabilities that integrate seamlessly with LINQ:

### Direct Search Methods

```csharp
// Search across all entities
var allResults = await graph.Search("machine learning").ToListAsync();

// Type-specific searches
var nodes = await graph.SearchNodes<Article>("artificial intelligence").ToListAsync();
var relationships = await graph.SearchRelationships<Knows>("college").ToListAsync();
```

### LINQ Integration

The `Search()` method can be used anywhere in a LINQ chain:

```csharp
// Search in basic LINQ chain
var results = await graph.Nodes<Person>()
    .Where(p => p.Age > 25)
    .Search("software engineer")
    .ToListAsync();

// Search in path segments traversal
var memories = await graph.Nodes<User>()
    .Where(u => u.Id == "...")
    .PathSegments<User, UserMemory, Memory>()
    .Select(p => p.EndNode)
    .Search("vacation memories")
    .ToListAsync();

// Search with multiple conditions
var filtered = await graph.Nodes<Article>()
    .Where(a => a.PublishedDate > DateTime.UtcNow.AddDays(-30))
    .Search("machine learning")
    .Where(a => a.Author.StartsWith("Dr."))
    .ToListAsync();
```

### Search Features

- **Case Insensitive**: All searches are case-insensitive
- **Multi-word Support**: Search for phrases like "machine learning"
- **Property Control**: Exclude properties with `[Property(IncludeInFullTextSearch = false)]`
- **Automatic Indexing**: Full-text indexes are managed automatically
- **LINQ Integration**: Use anywhere in query chains

## 🎯 Design Principles

### 1. Interface Segregation

- Small, focused interfaces with specific responsibilities
- Compose behavior rather than inherit it

### 2. Type Safety

- Strong typing throughout the API
- Generic constraints ensure proper usage

### 3. Async-First

- All I/O operations are asynchronous
- Proper cancellation token support

### 4. LINQ Integration

- Familiar LINQ syntax for querying
- Provider pattern for extensibility

### 5. Resource Management

- Proper disposal patterns
- Transaction scope management

Understanding these core interfaces is essential for building robust graph applications with Graph Model. Each interface is designed with specific concerns in mind, enabling clean, maintainable, and performant graph operations.

# Core Interfaces and Type System

This document provides a comprehensive guide to the core interfaces and type system that form the foundation of Graph Model. Understanding these interfaces is essential for effective use of the library.

## 🏗️ Architecture Overview

Graph Model follows a layered architecture with clean abstractions:

```
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

### Key Points:

- **Immutable after persistence**: Once saved to the graph, the `Id` should not change
- **String-based**: Uses strings for maximum flexibility across different graph databases
- **Required for all entities**: Both nodes and relationships must have unique identifiers

### Example Implementation:

```csharp
public class Person : INode
{
    // Good: Using init-only setter
    public string Id { get; init; } = Guid.NewGuid().ToString();

    // Alternative: Read-only after construction
    public string Id { get; private set; } = Guid.NewGuid().ToString();
}
```

## 🎯 Nodes: INode Interface

Nodes represent the primary entities in your graph model:

```csharp
/// <summary>
/// Defines the contract for node entities in the graph model.
/// Nodes represent primary data entities that can be connected via relationships.
/// This interface serves as a marker interface that extends IEntity,
/// signifying that implementing classes represent nodes rather than relationships.
/// </summary>
public interface INode : IEntity
{
    // Marker interface - no additional members beyond IEntity
}
```

### Design Philosophy:

- **Marker Interface**: `INode` doesn't add properties, it just identifies node types
- **Composition over Inheritance**: Add behavior through properties, not inheritance
- **Domain-Focused**: Your node classes represent your actual business entities

### Node Implementation Patterns:

#### 1. Simple Node

```csharp
[Node("Person")]
public class Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [Property("first_name", Index = true)]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name", Index = true)]
    public string LastName { get; set; } = string.Empty;

    [Property]
    public int Age { get; set; }

    // Computed property - excluded from persistence
    [Property(Ignore = true)]
    public string FullName => $"{FirstName} {LastName}";
}
```

#### 2. Node with Complex Properties

```csharp
[Node("Company")]
public class Company : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [Property]
    public string Name { get; set; } = string.Empty;

    [Property]
    public Address Headquarters { get; set; } = new(); // Complex type

    [Property]
    public List<Address> Offices { get; set; } = new(); // Collection

    [Property]
    public Dictionary<string, object> Metadata { get; set; } = new(); // Dynamic data
}

public class Address // Not a node - just a value object
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```

#### 3. Node Inheritance Hierarchy

```csharp
[Node("Person")]
public abstract class Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [Property("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Property("last_name")]
    public string LastName { get; set; } = string.Empty;

    [Property]
    public DateTime DateOfBirth { get; set; }
}

[Node("Employee", "Person")]
public class Employee : Person
{
    [Property("employee_id", Index = true)]
    public string EmployeeId { get; set; } = string.Empty;

    [Property]
    public string Department { get; set; } = string.Empty;

    [Property]
    public decimal Salary { get; set; }
}

[Node("Manager", "Employee", "Person")]
public class Manager : Employee
{
    [Property]
    public List<string> TeamMemberIds { get; set; } = new();

    [Property]
    public string ManagementLevel { get; set; } = string.Empty;
}
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
    /// Gets the direction of this relationship.
    /// The direction determines how the relationship can be traversed.
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

The `RelationshipDirection` enum controls traversal behavior:

```csharp
public enum RelationshipDirection
{
    Outgoing,    // Can be traversed from start to end
    Incoming,    // Can be traversed from end to start
    Bidirectional // Can be traversed in both directions
}
```

### Relationship Implementation Patterns:

#### 1. Simple Relationship

```csharp
[Relationship("KNOWS")]
public class Knows : IRelationship
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    [Property]
    public DateTime Since { get; set; }

    [Property]
    public string Type { get; set; } = "acquaintance";
}
```

#### 2. Strongly-Typed Relationship

```csharp
[Relationship("KNOWS")]
public class Knows : IRelationship<Person, Person>
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Bidirectional;

    // Navigation properties
    public Person? Source { get; set; }
    public Person? Target { get; set; }

    [Property]
    public DateTime Since { get; set; }

    [Property]
    public int Strength { get; set; } = 1; // 1-10 scale
}
```

#### 3. Business-Specific Relationships

```csharp
[Relationship("EMPLOYS")]
public class Employment : IRelationship<Company, Person>
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;

    public Company? Source { get; set; }  // Employer
    public Person? Target { get; set; }   // Employee

    [Property]
    public string Position { get; set; } = string.Empty;

    [Property]
    public DateTime StartDate { get; set; }

    [Property]
    public DateTime? EndDate { get; set; }

    [Property]
    public decimal Salary { get; set; }

    [Property]
    public EmploymentType Type { get; set; } = EmploymentType.FullTime;

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
    public NodeAttribute(string label) { }
    public NodeAttribute(params string[] labels) { } // Multiple labels

    public string Label { get; set; }
    public string[] AdditionalLabels { get; private set; }
}
```

Examples:

```csharp
[Node("Person")]                    // Single label
[Node("Employee", "Person")]        // Multiple labels
[Node("Manager", "Employee", "Person")] // Hierarchy
public class Manager : INode { }
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
    public RelationshipAttribute(params string[] labels) { }

    public string? Label { get; set; }
    public string[] AdditionalLabels { get; private set; }
}
```

Examples:

```csharp
[Relationship("KNOWS")]
[Relationship("MANAGES")]
[Relationship("REPORTS_TO")]
public class ReportsTo : IRelationship<Person, Person> { }
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
public class Person : INode
{
    [Property(Label = "first_name")]        // Custom name
    public string FirstName { get; set; } = string.Empty;

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
public interface IGraph : IAsyncDisposable
{
    // Queryable interfaces for LINQ support
    IGraphNodeQueryable<N> Nodes<N>(IGraphTransaction? transaction = null) where N : INode;
    IGraphRelationshipQueryable<R> Relationships<R>(IGraphTransaction? transaction = null) where R : IRelationship;

    // CRUD operations
    Task<N> GetNodeAsync<N>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode;
    Task<R> GetRelationshipAsync<R>(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship;

    Task CreateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode;
    Task CreateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship;

    Task UpdateNodeAsync<N>(N node, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where N : INode;
    Task UpdateRelationshipAsync<R>(R relationship, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default) where R : IRelationship;

    Task DeleteNodeAsync(string id, bool cascadeDelete = false, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteRelationshipAsync(string id, IGraphTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Transaction management
    Task<IGraphTransaction> GetTransactionAsync();
}
```

## 📊 Query Interfaces

### IGraphQueryable<T>

The foundation of LINQ support in Graph Model:

```csharp
/// <summary>
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends IQueryable&lt;T&gt; with additional functionality specific to graph operations.
/// </summary>
/// <typeparam name="T">The type of data being queried.</typeparam>
public interface IGraphQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>
{
    new IGraphQueryProvider Provider { get; }

    // Graph-specific extensions
    IGraphQueryable<T> WithTransaction(IGraphTransaction transaction);
    IGraphQueryable<T> WithDepth(int maxDepth);
    IGraphQueryable<T> WithDepth(int minDepth, int maxDepth);
    IGraphQueryable<T> Direction(GraphTraversalDirection direction);
}
```

### Specialized Queryable Interfaces

```csharp
/// <summary>
/// Queryable interface specifically for graph nodes with traversal capabilities.
/// </summary>
public interface IGraphNodeQueryable<TNode> : IGraphQueryable<TNode>, IGraphNodeQueryable
    where TNode : INode
{
    // Node-specific operations will be added here
}

/// <summary>
/// Queryable interface specifically for graph relationships.
/// </summary>
public interface IGraphRelationshipQueryable<TRelationship> : IGraphQueryable<TRelationship>, IGraphRelationshipQueryable
    where TRelationship : IRelationship
{
    // Relationship-specific operations will be added here
}
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
    Task Commit(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all changes made in this transaction.
    /// </summary>
    Task Rollback(CancellationToken cancellationToken = default);
}
```

### Transaction Usage Patterns:

```csharp
// Pattern 1: Explicit transaction management
await using var transaction = await graph.GetTransactionAsync();
try
{
    await graph.CreateNodeAsync(node, transaction: transaction);
    await graph.CreateRelationshipAsync(relationship, transaction: transaction);
    await transaction.Commit();
}
catch
{
    await transaction.Rollback();
    throw;
}

// Pattern 2: Using statement with automatic rollback
await using var transaction = await graph.GetTransactionAsync();
await graph.CreateNodeAsync(node, transaction: transaction);
await graph.CreateRelationshipAsync(relationship, transaction: transaction);
await transaction.Commit(); // Must explicitly commit

// Pattern 3: Extension method for convenience (if available)
await graph.WithTransactionAsync(async (g, tx) =>
{
    await g.CreateNodeAsync(node, transaction: tx);
    await g.CreateRelationshipAsync(relationship, transaction: tx);
    // Automatically committed if no exception
});
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

## 📚 Next Steps

- **[Getting Started](getting-started.md)** - Practical examples using these interfaces
- **[Advanced Querying](querying.md)** - LINQ patterns and graph traversal
- **[Transaction Management](transactions.md)** - Advanced transaction patterns
- **[Best Practices](best-practices.md)** - Design and performance guidelines

Understanding these core interfaces is essential for building robust graph applications with Graph Model. Each interface is designed with specific concerns in mind, enabling clean, maintainable, and performant graph operations.

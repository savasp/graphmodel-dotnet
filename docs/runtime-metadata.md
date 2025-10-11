# Runtime Metadata Properties

This document explains the runtime metadata properties added to `INode` and `IRelationship` interfaces and how they enable more flexible querying while maintaining type safety.

## Overview

The Graph Model library now includes runtime metadata properties on the core interfaces:

- **`INode.Labels`**: Provides access to the node's labels as stored in the database
- **`IRelationship.Type`**: Provides access to the relationship's type as stored in the database

These properties enable polymorphic queries and filtering at runtime while complementing the compile-time type system.

## The INode.Labels Property

### Interface Definition

```csharp
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

### Key Points

1. **Read-Only**: The property only has a getter; it cannot be set manually
2. **Provider-Managed**: The graph provider populates this property during serialization/deserialization
3. **Derived from Attributes**: Labels come from `NodeAttribute` or the type name if no attribute is present
4. **Enables Runtime Queries**: Allows filtering by label without knowing the compile-time type

### Example Usage

```csharp
// Query nodes and filter by runtime labels
var adminUsers = graph.Nodes<User>()
    .Where(u => u.Labels.Contains("Admin"))
    .ToList();

// Filter in path traversal
var query = graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .PathSegments<User, IRelationship, INode>()
    .Where(ps => ps.EndNode.Labels.Contains("Memory"))
    .ToList();
```

## The IRelationship.Type Property

### Interface Definition

```csharp
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

    // ... other properties ...
}
```

### Key Points

1. **Read-Only**: The property only has a getter; it cannot be set manually
2. **Provider-Managed**: The graph provider populates this property during serialization/deserialization
3. **Derived from Attributes**: Type comes from `RelationshipAttribute` or the type name if no attribute is present
4. **Enables Runtime Queries**: Allows filtering by type without knowing the compile-time type

### Example Usage

```csharp
// Filter relationships by type in path traversal
var query = graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .PathSegments<User, UserMemory, Memory>()
    .Where(ps => ps.EndNode.Id == memoryId && ps.Relationship.Type == "REMEMBERS")
    .ToList();

// Filter polymorphic relationships
var adminRelationships = graph.Relationships<IRelationship>()
    .Where(r => r.Type.StartsWith("ADMIN_"))
    .ToList();
```

## Using the Node and Relationship Base Classes

### Why Use Base Classes?

The `Node` and `Relationship` base classes provide:

1. **Automatic ID Generation**: No need to manually create GUIDs
2. **Runtime Metadata Management**: The `Labels` and `Type` properties are automatically initialized
3. **Correct Initialization**: Base classes ensure proper initialization patterns
4. **Analyzer Support**: The GM011 analyzer warns when interfaces are implemented directly

### The Node Base Class

```csharp
/// <summary>
/// Base class for graph nodes that provides a default implementation of the INode interface.
/// </summary>
public abstract record Node : INode
{
    /// <summary>
    /// Gets or sets the unique identifier of this node.
    /// Automatically initialized with a new GUID string when a node is created.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the labels for this node as they are stored in the graph database.
    /// </summary>
    public virtual IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
}
```

#### Example

```csharp
[Node("Person")]
public record Person : Node  // Inherit from Node
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
```

### The Relationship Base Class

```csharp
/// <summary>
/// Base class for graph relationships that provides a default implementation of the IRelationship interface.
/// </summary>
public abstract record Relationship(
    string StartNodeId,
    string EndNodeId,
    RelationshipDirection Direction = RelationshipDirection.Outgoing) : IRelationship
{
    /// <inheritdoc/>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the type of this relationship as it is stored in the graph database.
    /// </summary>
    public virtual string Type { get; init; } = string.Empty;
}
```

#### Example

```csharp
[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
}
```

## Analyzer Rule: GM011

The `GM011` analyzer rule warns when types directly implement `INode` or `IRelationship` without inheriting from the base classes:

### Rule Details

- **ID**: GM011
- **Category**: Graph.Model
- **Severity**: Warning
- **Message**: "Type '{TypeName}' should inherit from '{BaseClass}' instead of implementing '{Interface}' directly. The base class provides default implementations for runtime metadata properties"

### Why This Rule Exists

1. **Prevents Manual Metadata Management**: Users shouldn't manually populate `Labels` or `Type`
2. **Ensures Consistency**: All nodes and relationships follow the same pattern
3. **Simplifies Code**: No need to write boilerplate for ID generation and metadata
4. **Future-Proof**: Base classes can evolve to provide additional functionality

### Example

```csharp
// ❌ Triggers GM011 warning
public record Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public IReadOnlyList<string> Labels { get; } = new List<string>(); // Don't do this!
    public string Name { get; set; } = string.Empty;
}

// ✅ Recommended approach
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
}
```

## Implementation Details

### Serialization

During serialization (when saving to the database):

1. The code generator examines the entity type
2. It looks for `NodeAttribute` or `RelationshipAttribute` to determine labels/type
3. It reads the entity's `Labels` or `Type` property if already populated
4. It creates an `EntityInfo` with the appropriate metadata
5. The provider uses this information to create the database entity

### Deserialization

During deserialization (when loading from the database):

1. The provider reads the node labels or relationship type from the database
2. It creates an `EntityInfo` with this metadata
3. The code generator populates the entity's `Labels` or `Type` property
4. The entity is returned with runtime metadata correctly set

### Code Generator Updates

The serialization code generator (`Serialization.cs`) was updated to:

1. For nodes: Populate `EntityInfo.ActualLabels` from `entity.Labels`
2. For relationships: Populate `EntityInfo.Label` from `entity.Type`
3. Handle cases where labels/type are not yet populated (fallback to attributes)

### Neo4j Provider Updates

The `CypherResultProcessor` was updated to:

1. Add `Labels` as a `SimpleCollection` property when creating `EntityInfo` from nodes
2. Add `Type` as a `SimpleValue` property when creating `EntityInfo` from relationships
3. Ensure these properties are populated during deserialization

## Migration Guide

If you have existing code that directly implements `INode` or `IRelationship`:

### Before

```csharp
public class Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}

public class Knows : IRelationship
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string StartNodeId { get; init; } = string.Empty;
    public string EndNodeId { get; init; } = string.Empty;
    public RelationshipDirection Direction { get; init; } = RelationshipDirection.Outgoing;
    public DateTime Since { get; set; }
}
```

### After

```csharp
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
}

[Relationship("KNOWS")]
public record Knows(string StartNodeId, string EndNodeId) : Relationship(StartNodeId, EndNodeId)
{
    public DateTime Since { get; set; }
}
```

## Best Practices

1. **Always use base classes**: Inherit from `Node` or `Relationship` instead of implementing interfaces directly
2. **Never set Labels or Type manually**: These are managed by the graph provider
3. **Use for queries, not mutations**: Read these properties for filtering, don't try to change them
4. **Install analyzers**: The `Cvoya.Graph.Model.Analyzers` package helps catch incorrect usage
5. **Use records**: C# records work well with the base classes for concise syntax

## Summary

The runtime metadata properties (`Labels` and `Type`) provide a bridge between the compile-time type system and runtime graph queries. By using the base classes and following the recommended patterns, you get:

- **Type Safety**: Compile-time checking of your domain model
- **Runtime Flexibility**: Filter and query by labels/types without knowing compile-time types
- **Provider Management**: No manual metadata management required
- **Analyzer Support**: Catch incorrect usage at compile time

This design enables powerful polymorphic queries while maintaining the clean separation between your domain model and the graph database structure.

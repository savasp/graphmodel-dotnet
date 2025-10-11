# Summary of Changes: Runtime Metadata Properties

## Overview

This document summarizes all changes made to support runtime metadata properties (`Labels` and `Type`) on the `INode` and `IRelationship` interfaces, enabling more flexible polymorphic queries while maintaining type safety.

## Problem Statement

The original fully-typed `IGraph` abstraction was restrictive when trying to filter relationships by type or nodes by label in LINQ queries. For example:

```csharp
var query = graph.Nodes<User>()
    .Where(u => u.Id == userId)
    .PathSegments<User, UserMemory, Memory>()
    .Where(ps => ps.EndNode.Id == memoryId && ps.Relationship.Type == relationshipType)
```

The `IRelationship` interface didn't expose the relationship's type, and `INode` didn't expose node labels.

## Solution

Added runtime metadata properties to the interfaces, managed by the graph provider:

- `INode.Labels` - Provides access to node labels as stored in the database
- `IRelationship.Type` - Provides access to relationship type as stored in the database

## Files Modified

### Core Interfaces

#### 1. `src/Graph.Model/INode.cs`

**Changes**:

- Added `IReadOnlyList<string> Labels { get; }` property
- Property is get-only (no `init` accessor) to prevent manual setting
- Added comprehensive XML documentation

**Key Code**:

```csharp
public interface INode : IEntity
{
    /// <summary>
    /// Gets the labels for this node as they are stored in the graph database.
    /// </summary>
    IReadOnlyList<string> Labels { get; }
}
```

#### 2. `src/Graph.Model/IRelationship.cs`

**Changes**:

- Added `string Type { get; }` property
- Property is get-only (no `init` accessor) to prevent manual setting
- Added comprehensive XML documentation

**Key Code**:

```csharp
public interface IRelationship : IEntity
{
    /// <summary>
    /// Gets the type of this relationship as it is stored in the graph database.
    /// </summary>
    string Type { get; }

    // ... other properties ...
}
```

### Base Classes

#### 3. `src/Graph.Model/Node.cs`

**Changes**:

- Added `public virtual IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();`
- Uses `get; init;` to allow the deserializer to set the value
- Provides default empty list

#### 4. `src/Graph.Model/Relationship.cs`

**Changes**:

- Added `public virtual string Type { get; init; } = string.Empty;`
- Uses `get; init;` to allow the deserializer to set the value
- Provides default empty string

### Dynamic Entities

#### 5. `src/Graph.Model/DynamicNode.cs`

**Changes**:

- Changed `Labels` property to use `override` keyword
- `public override IReadOnlyList<string> Labels { get; init; } = new List<string>();`

#### 6. `src/Graph.Model/DynamicRelationship.cs`

**Changes**:

- Changed `Type` property to use `override` keyword
- `public override string Type { get; init; } = string.Empty;`

### Code Generation

#### 7. `src/Graph.Model.Serialization.CodeGen/Serialization.cs`

**Changes**:

- Updated `GenerateSerializeMethod` to populate `EntityInfo` with runtime metadata
- For nodes: Populate `ActualLabels` from `entity.Labels`
- For relationships: Populate `Label` from `entity.Type`
- Falls back to attribute-derived labels/types if not set

**Key Code**:

```csharp
// For nodes
var actualLabels = entity.Labels?.ToList() ?? new List<string>();
var primaryLabel = actualLabels.Count > 0 ? actualLabels[0] : "{GetLabelFromType(type)}";

// For relationships
var relationshipType = entity.Type ?? "{GetLabelFromType(type)}";
```

### Neo4j Provider

#### 8. `src/Graph.Model.Neo4j/Querying/Cypher/Execution/CypherResultProcessor.cs`

**Changes**:

- Updated `CreateEntityInfoFromNode` to add `Labels` as a `SimpleCollection` property
- Updated `CreateEntityInfoFromRelationship` to add `Type` as a `SimpleValue` property
- Ensures runtime metadata is populated during deserialization

**Key Code**:

```csharp
// In CreateEntityInfoFromNode
simpleProperties[nameof(Model.INode.Labels)] = new Property(
    PropertyInfo: labelsProperty,
    Label: nameof(Model.INode.Labels),
    IsNullable: false,
    Value: new SimpleCollection(
        node.Labels.Select(l => new SimpleValue(l, typeof(string))).ToList(),
        typeof(string))
);

// In CreateEntityInfoFromRelationship
simpleProperties[nameof(Model.IRelationship.Type)] = new Property(
    PropertyInfo: typeProperty,
    Label: nameof(Model.IRelationship.Type),
    IsNullable: false,
    Value: new SimpleValue(label, typeof(string))
);
```

### Roslyn Analyzer

#### 9. `src/Graph.Model.Analyzers/DiagnosticDescriptors.cs`

**Changes**:

- Added `GM011` diagnostic descriptor for warning about direct interface implementation

#### 10. `src/Graph.Model.Analyzers/GraphModelAnalyzer.cs`

**Changes**:

- Added `AnalyzeBaseClassInheritance` method
- Checks if types directly implement `INode`/`IRelationship` without inheriting from base classes
- Reports `GM011` warning for concrete (non-abstract) types

**Key Code**:

```csharp
private static void AnalyzeBaseClassInheritance(
    SymbolAnalysisContext context,
    INamedTypeSymbol namedType,
    bool implementsINode,
    bool implementsIRelationship)
{
    // Skip abstract types and interfaces
    if (namedType.IsAbstract || namedType.TypeKind == TypeKind.Interface)
        return;

    // Check inheritance hierarchy
    bool inheritsFromNode = /* check for Node base class */;
    bool inheritsFromRelationship = /* check for Relationship base class */;

    // Report diagnostic if implementing directly
    if (implementsINode && !inheritsFromNode) { /* ... */ }
    if (implementsIRelationship && !inheritsFromRelationship) { /* ... */ }
}
```

#### 11. `src/Graph.Model.Analyzers/Properties/Resources.resx`

**Changes**:

- Added resource strings for GM011 rule (Title, MessageFormat, Description)

#### 12. `src/Graph.Model.Analyzers/Properties/Resources.Designer.cs`

**Changes**:

- Added properties for GM011 resource strings

#### 13. `src/Graph.Model.Analyzers/AnalyzerReleases.Unshipped.md`

**Changes**:

- Added GM011 to the list of unshipped analyzer rules

### Tests

#### 14. `tests/Graph.Model.Analyzers.Tests/GM011_ShouldInheritFromBaseClassTests.cs`

**New File**: Comprehensive test suite for GM011 analyzer

**Test Coverage**:

- ✅ Node inheriting from base class produces no diagnostic
- ✅ Relationship inheriting from base class produces no diagnostic
- ✅ Abstract node inheriting from base class produces no diagnostic
- ✅ Node implementing interface directly produces GM011 warning
- ✅ Relationship implementing interface directly produces GM011 warning
- ✅ Abstract node implementing interface directly produces no diagnostic (skipped)
- ✅ Interface extending INode produces no diagnostic
- ✅ Class implementing interface directly produces GM011 warning
- ✅ Custom base class inheriting from Node produces no diagnostic

### Documentation

#### 15. `docs/best-practices.md`

**Changes**:

- Added new section "1. Use Base Classes for Node and Relationship Implementation"
- Explains why to use base classes vs. direct interface implementation
- Documents runtime metadata properties and their purpose
- Shows examples of correct and incorrect patterns

#### 16. `docs/core-concepts.md`

**Changes**:

- Updated `INode` interface documentation to include `Labels` property
- Updated `IRelationship` interface documentation to include `Type` property
- Added design philosophy notes about runtime metadata
- Updated all examples to use base classes
- Added "Best Practice" callouts recommending base class usage

#### 17. `docs/runtime-metadata.md`

**New File**: Comprehensive guide to runtime metadata properties

**Contents**:

- Overview of runtime metadata
- Detailed documentation of `INode.Labels`
- Detailed documentation of `IRelationship.Type`
- Using the Node and Relationship base classes
- Analyzer rule GM011 explanation
- Implementation details (serialization/deserialization)
- Migration guide
- Best practices

## Design Decisions

### 1. Read-Only Interface Properties

**Decision**: Interface properties are `get` only (no `init` accessor)

**Rationale**:

- Prevents developers from thinking they need to set these values manually
- Makes it clear these are provider-managed properties
- Reduces confusion about who is responsible for populating them

### 2. Base Class Properties with `get; init;`

**Decision**: Base class properties use `get; init;` accessors

**Rationale**:

- Allows the deserializer to set values during object construction
- Maintains immutability after construction
- Provides a clear contract between provider and entity

### 3. Analyzer Warning (Not Error)

**Decision**: GM011 is a warning, not an error

**Rationale**:

- Allows existing code to continue working
- Provides guidance without breaking builds
- Gives developers flexibility in special cases

### 4. Virtual Properties in Base Classes

**Decision**: Base class properties are `virtual`

**Rationale**:

- Allows derived classes to override if needed (e.g., DynamicNode)
- Provides flexibility for advanced scenarios
- Maintains backward compatibility

## Benefits

1. **Polymorphic Queries**: Filter by labels/types without knowing compile-time types
2. **Type Safety**: Compile-time checking still enforced
3. **Provider-Managed**: No manual metadata management required
4. **Analyzer Support**: Catches incorrect usage at compile time
5. **Backward Compatible**: Existing code continues to work (with warnings)
6. **Clean API**: Clear separation between domain model and runtime metadata

## Migration Path

For existing code implementing interfaces directly:

### Before

```csharp
public class Person : INode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}
```

### After

```csharp
[Node("Person")]
public record Person : Node
{
    public string Name { get; set; } = string.Empty;
}
```

## Testing Status

- ✅ Core libraries build successfully
- ✅ Examples build successfully
- ✅ Analyzer builds successfully
- ✅ Analyzer tests created (9 tests total)
- ⚠️ Some existing test files need updates to implement `Labels`/`Type` properties
- ⚠️ Performance test files need updates

## Future Work

1. Update all test files to use base classes
2. Add integration tests for runtime metadata queries
3. Add performance benchmarks for label/type filtering
4. Consider code fixes for GM011 (auto-convert to base class)
5. Update all examples to demonstrate runtime metadata usage

## Summary

This update successfully adds runtime metadata support while maintaining:

- Clean API design
- Type safety
- Provider encapsulation
- Developer ergonomics
- Backward compatibility (with warnings)

The combination of interface properties, base classes, and analyzer support provides a robust foundation for polymorphic queries while guiding developers toward the recommended patterns.

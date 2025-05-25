# Example 4: Advanced Scenarios

This example demonstrates advanced features of the GraphModel library including polymorphism, multiple relationship types, and complex graph patterns.

## What You'll Learn

- Working with polymorphic node types (inheritance)
- Managing multiple relationship types between nodes
- Complex queries across type hierarchies
- Pattern matching in graphs
- Conditional updates based on graph structure

## Key Concepts Demonstrated

### 1. Polymorphic Types

```csharp
// Base type
[Node("Content")]
public abstract class Content : Node { }

// Derived types
[Node("Article")]
public class Article : Content { }

[Node("Video")]
public class Video : Content { }
```

### 2. Multiple Relationship Types

Different relationships can exist between the same or different node types:

```csharp
[Relationship("CONTAINS")]
public class Contains : Relationship<Blog, Content> { }

[Relationship("REFERENCES")]
public class References : Relationship<Content, Content> { }

[Relationship("TAGGED_WITH")]
public class TaggedWith : Relationship<Content, Tag> { }
```

### 3. Complex Navigation Properties

```csharp
// Outgoing relationships
public IEnumerable<References> References => GetRelationships<References>();

// Incoming relationships
public IEnumerable<Contains> ContainedIn => GetIncomingRelationships<Contains>();
```

### 4. Type-Safe Queries Across Hierarchies

```csharp
// Query base type
var allContent = graph.Nodes<Content>().ToList();

// Query specific type
var articles = graph.Nodes<Article>()
    .Where(a => a.WordCount > 1000)
    .ToList();
```

### 5. Pattern Matching

Find complex patterns in the graph:

```csharp
// Content that is both contained in a blog and has tags
var pattern = graph.Nodes<Content>(new GraphOperationOptions().WithDepth(1))
    .Where(c => c.ContainedIn.Any() && c.Tags.Any())
    .ToList();
```

## Content Management Example

The example simulates a content management system with:

- Blogs containing articles
- Articles referencing videos
- Content tagged with categories
- View tracking and statistics

## Running the Example

```bash
cd examples/Example4.AdvancedScenarios
dotnet run
```

Make sure Neo4j is running and accessible at `neo4j://localhost:7687`.

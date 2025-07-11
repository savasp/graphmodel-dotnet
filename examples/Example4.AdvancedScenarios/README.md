# Example 4: Advanced Scenarios

This example demonstrates advanced features of the GraphModel library including polymorphic node types, multiple relationship types, and complex graph operations.

## What You'll Learn

- Working with polymorphic node types (inheritance) 
- Managing multiple relationship types between nodes
- Basic queries across type hierarchies
- Simple graph traversal patterns
- Conditional updates based on node properties

## Key Concepts Demonstrated

### 1. Polymorphic Node Types

```csharp
// Base type
[Node(Label = "Content")]
public record Content : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

// Derived types
[Node(Label = "Article")]
public record Article : Content
{
    public DateTime PublishedDate { get; set; }
    public int WordCount { get; set; }
}

[Node(Label = "Video")]
public record Video : Content
{
    public int Duration { get; set; }
    public int Views { get; set; }
}
```

### 2. Multiple Relationship Types

Different relationships can exist between different node types:

```csharp
[Relationship(Label = "CONTAINS")]
public record Contains(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)

[Relationship(Label = "REFERENCES")]
public record References(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public string Context { get; set; } = string.Empty;
}

[Relationship(Label = "TAGGED_WITH")]
public record TaggedWith(string startNodeId, string endNodeId) : Relationship(startNodeId, endNodeId)
{
    public string TagName { get; set; } = string.Empty;
}
```

### 3. Type-Safe Queries Across Hierarchies

```csharp
// Query base type - gets all content types
var allContent = graph.Nodes<Content>().ToList();

// Query specific type with filtering
var articles = graph.Nodes<Article>()
    .Where(a => a.WordCount > 1000)
    .ToList();
```

### 4. Path Traversal

```csharp
// Find content with their tags
var taggedContent = await graph.Nodes<Content>()
    .PathSegments<Content, TaggedWith, Tag>()
    .ToListAsync();
```

## Content Management Example

The example simulates a content management system with:

- Blogs containing articles
- Articles referencing videos  
- Content tagged with categories
- View tracking and popularity metrics

## Running the Example

**Note: This example requires .NET 10.0 which is not yet released.**

```bash
cd examples/Example4.AdvancedScenarios
dotnet run
```

Make sure Neo4j is running and accessible at `bolt://localhost:7687` with username `neo4j` and password `password`.

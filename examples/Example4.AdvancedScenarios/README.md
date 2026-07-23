# Example 4: Advanced scenarios

This example uses .NET 10 and C# 14 to demonstrate polymorphic node types, multiple relationship
types, grouped queries, traversal, and conditional set-based updates.

## Polymorphic model

```csharp
[Node(Label = "Content")]
public record Content : Node
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

[Node(Label = "Article")]
public record Article : Content
{
    public DateTime PublishedDate { get; set; }
    public int WordCount { get; set; }
}

[Relationship(Label = "TAGGED_WITH")]
public record TaggedWith : Relationship
{
    public string TagName { get; set; } = string.Empty;
}
```

Relationships carry domain properties only; creation receives endpoint selections separately:

```csharp
await graph.CreateRelationshipAsync(
    graph.Nodes<Article>().Where(article => article.Url == aiArticle.Url),
    new TaggedWith { TagName = "AI" },
    graph.Nodes<Tag>().Where(tag => tag.Name == aiTag.Name));
```

The example queries polymorphic content and path segments, then updates matching videos:

```csharp
var taggedContent = await graph.Nodes<Content>()
    .PathSegments<Content, TaggedWith, Tag>()
    .ToListAsync();

await graph.Nodes<Video>()
    .Where(video => video.Views > 10_000)
    .UpdateAsync(setters => setters
        .SetProperty(video => video.Views, video => video.Views + 100));
```

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example4.AdvancedScenarios
```

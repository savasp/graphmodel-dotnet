// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 4: Advanced Scenarios
// Demonstrates advanced features like polymorphism, multiple relationship types, and complex queries

Console.WriteLine("=== Example 4: Advanced Scenarios ===\n");

const string databaseName = "example4";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession())
{
    await session.RunAsync($"CREATE OR REPLACE DATABASE {databaseName}");
}

Console.WriteLine($"✓ Created database: {databaseName}");

// Create graph instance with Neo4j provider
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
});

var store = new Neo4jGraphStore("bolt://localhost:7687", "neo4j", "password", databaseName, null, loggerFactory);
var graph = store.Graph;
try
{
    // ==== POLYMORPHIC NODES ====
    Console.WriteLine("1. Creating content with polymorphic nodes...");

    var techBlog = new Blog
    {
        Title = "Tech Insights",
        Url = "https://techinsights.blog",
        Author = "Alice Tech",
        Category = "Technology"
    };

    var aiArticle = new Article
    {
        Title = "Understanding Large Language Models",
        Url = "https://techinsights.blog/llm-guide",
        Author = "Alice Tech",
        PublishedDate = DateTime.UtcNow.AddDays(-7),
        WordCount = 2500
    };

    var mlVideo = new Video
    {
        Title = "Machine Learning Fundamentals",
        Url = "https://youtube.com/watch?v=ml101",
        Author = "Bob Teacher",
        Duration = 45,
        Views = 15000
    };

    await graph.CreateNodeAsync(techBlog);
    await graph.CreateNodeAsync(aiArticle);
    await graph.CreateNodeAsync(mlVideo);

    Console.WriteLine($"✓ Created blog: {techBlog.Title}");
    Console.WriteLine($"✓ Created article: {aiArticle.Title}");
    Console.WriteLine($"✓ Created video: {mlVideo.Title}\n");

    // ==== MULTIPLE RELATIONSHIP TYPES ====
    Console.WriteLine("2. Creating multiple relationship types...");

    // Blog contains article
    await graph.CreateRelationshipAsync(new Contains(techBlog.Id, aiArticle.Id));

    // Article references video
    await graph.CreateRelationshipAsync(new References(aiArticle.Id, mlVideo.Id)
    {
        Context = "See this video for visual explanation"
    });

    // Create tags
    var aiTag = new Tag { Name = "Artificial Intelligence" };
    var mlTag = new Tag { Name = "Machine Learning" };

    await graph.CreateNodeAsync(aiTag);
    await graph.CreateNodeAsync(mlTag);

    // Tag content
    await graph.CreateRelationshipAsync(new TaggedWith(aiArticle.Id, aiTag.Id) { TagName = "AI" });
    await graph.CreateRelationshipAsync(new TaggedWith(aiArticle.Id, mlTag.Id) { TagName = "ML" });
    await graph.CreateRelationshipAsync(new TaggedWith(mlVideo.Id, mlTag.Id) { TagName = "ML" });

    Console.WriteLine("✓ Created relationships between content");
    Console.WriteLine("✓ Created and applied tags\n");

    // ==== QUERYING POLYMORPHIC TYPES ====
    Console.WriteLine("3. Querying polymorphic content...");

    // Query all content (base type)
    var allContent = graph.Nodes<Content>().ToList();
    Console.WriteLine($"Total content items: {allContent.Count}");
    foreach (var content in allContent)
    {
        Console.WriteLine($"  - [{content.GetType().Name}] {content.Title} by {content.Author}");
    }

    // Query specific types
    var articles = graph.Nodes<Article>()
        .Where(a => a.WordCount > 1000)
        .ToList();

    Console.WriteLine($"\nArticles with 1000+ words: {articles.Count}");
    foreach (var article in articles)
    {
        Console.WriteLine($"  - {article.Title} ({article.WordCount} words)");
    }

    // ==== BASIC TRAVERSAL ====
    Console.WriteLine("\n4. Basic graph traversal...");

    // Find content with tags
    var taggedContent = await graph.Nodes<Content>()
        .PathSegments<Content, TaggedWith, Tag>()
        .ToListAsync();

    Console.WriteLine($"Content with tags:");
    foreach (var path in taggedContent)
    {
        Console.WriteLine($"  - {path.StartNode.Title} tagged with '{path.EndNode.Name}'");
    }

    // ==== AGGREGATION QUERIES ====
    Console.WriteLine("\n5. Aggregation and statistics...");

    // Group content by author
    var contentByAuthor = graph.Nodes<Content>()
        .GroupBy(c => c.Author)
        .Select(g => new { Author = g.Key, Count = g.Count() })
        .ToList();

    Console.WriteLine("Content by author:");
    foreach (var authorStats in contentByAuthor)
    {
        Console.WriteLine($"  - {authorStats.Author}: {authorStats.Count} items");
    }

    // Find most viewed video
    var mostViewedVideo = graph.Nodes<Video>()
        .OrderByDescending(v => v.Views)
        .FirstOrDefault();

    if (mostViewedVideo != null)
    {
        Console.WriteLine($"\nMost viewed video: {mostViewedVideo.Title} ({mostViewedVideo.Views:N0} views)");
    }

    // ==== CONDITIONAL UPDATES ====
    Console.WriteLine("\n6. Conditional updates based on content properties...");

    // Update view count for popular videos
    var popularVideos = graph.Nodes<Video>()
        .Where(v => v.Views > 10000)
        .ToList();

    foreach (var video in popularVideos)
    {
        video.Views += 100; // Boost views
        await graph.UpdateNodeAsync(video);
        Console.WriteLine($"✓ Boosted views for '{video.Title}' to {video.Views:N0}");
    }

    Console.WriteLine("\n=== Example 4 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Polymorphic node types with inheritance");
    Console.WriteLine("• Multiple relationship types between nodes");
    Console.WriteLine("• Basic queries across type hierarchies");
    Console.WriteLine("• Simple graph traversal patterns");
    Console.WriteLine("• Conditional updates based on node properties");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        Console.WriteLine(ex.InnerException.StackTrace);
    }
    Console.WriteLine("Make sure Neo4j is running on localhost:7687 with username 'neo4j' and password 'password'");
}
finally
{
    await graph.DisposeAsync();
    await using (var session = driver.AsyncSession())
    {
        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}
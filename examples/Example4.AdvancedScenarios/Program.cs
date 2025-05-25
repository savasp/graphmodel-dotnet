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

using Cvoya.Graph.Provider.Neo4j;
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

// We start with the Neo4j Graph Provider here

// Create graph instance with Neo4j provider
var graph = new Neo4jGraphProvider("bolt://localhost:7687", "neo4j", "password", databaseName, null);

// TODO: Some of the functionality in this example is isn't implemented properly.
/*
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

    await graph.CreateNode(techBlog);
    await graph.CreateNode(aiArticle);
    await graph.CreateNode(mlVideo);

    Console.WriteLine($"✓ Created blog: {techBlog.Title}");
    Console.WriteLine($"✓ Created article: {aiArticle.Title}");
    Console.WriteLine($"✓ Created video: {mlVideo.Title}\n");

    // ==== MULTIPLE RELATIONSHIP TYPES ====
    Console.WriteLine("2. Creating multiple relationship types...");

    // Blog contains article
    await graph.CreateRelationship(new Contains { Source = techBlog, Target = aiArticle });

    // Article references video
    await graph.CreateRelationship(new References
    {
        Source = aiArticle,
        Target = mlVideo,
        Context = "See this video for visual explanation"
    });

    // Create tags
    var aiTag = new Tag { Name = "Artificial Intelligence" };
    var mlTag = new Tag { Name = "Machine Learning" };

    await graph.CreateNode(aiTag);
    await graph.CreateNode(mlTag);

    // Tag content
    await graph.CreateRelationship(new TaggedWith { Source = aiArticle, Target = aiTag });
    await graph.CreateRelationship(new TaggedWith { Source = aiArticle, Target = mlTag });
    await graph.CreateRelationship(new TaggedWith { Source = mlVideo, Target = mlTag });

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

    // ==== COMPLEX TRAVERSAL ====
    Console.WriteLine("\n4. Complex graph traversal...");

    // Find all content tagged with "Machine Learning" and related content
    var mlContent = graph.Nodes<Tag>(new GraphOperationOptions().WithDepth(2))
        .Where(t => t.Name == "Machine Learning")
        .Select(t => t.TaggedContent.Target)
        .ToList();

    Console.WriteLine($"Content tagged with 'Machine Learning':");
    foreach (var content in mlContent)
    {
        Console.WriteLine($"  - {content?.Title}");

        // Check if it's an article with references
        if (content is Article article)
        {
            var refs = graph.Relationships<References>(new GraphOperationOptions().WithDepth(1))
                .Where(r => r.Source!.Id == article.Id)
                .ToList();

            foreach (var reference in refs)
            {
                Console.WriteLine($"    → References: {reference.Target?.Title}");
            }
        }
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

    // ==== PATTERN MATCHING ====
    Console.WriteLine("\n6. Pattern matching in the graph...");

    // Find content that is both contained in a blog and has tags
    var blogContent = graph.Nodes<Content>(new GraphOperationOptions().WithDepth(1))
        .Where(c => c.ContainedIn.Any() && c.Tags.Any())
        .ToList();

    Console.WriteLine("Content that is in a blog and has tags:");
    foreach (var content in blogContent)
    {
        var blog = content.ContainedIn.FirstOrDefault()?.Source;
        var tags = content.Tags.Select(t => t.Target?.Name);
        Console.WriteLine($"  - {content.Title} in '{blog?.Title}' with tags: {string.Join(", ", tags)}");
    }

    // ==== CONDITIONAL UPDATES ====
    Console.WriteLine("\n7. Conditional updates based on graph state...");

    // Update view count for videos referenced by popular articles
    var popularArticles = graph.Nodes<Article>(new GraphOperationOptions().WithDepth(1))
        .Where(a => a.WordCount > 2000)
        .ToList();

    foreach (var article in popularArticles)
    {
        var referencedVideos = article.References
            .Select(r => r.Target)
            .OfType<Video>()
            .ToList();

        foreach (var video in referencedVideos)
        {
            video.Views += 100; // Boost views
            await graph.UpdateNode(video);
            Console.WriteLine($"✓ Boosted views for '{video.Title}' (referenced by popular article)");
        }
    }

    Console.WriteLine("\n=== Example 4 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Polymorphic node types with inheritance");
    Console.WriteLine("• Multiple relationship types between nodes");
    Console.WriteLine("• Complex queries across type hierarchies");
    Console.WriteLine("• Graph pattern matching");
    Console.WriteLine("• Conditional updates based on graph structure");
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
    graph.Dispose();
    await using (var session = driver.AsyncSession())
    {
        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    driver.Dispose();
}
*/
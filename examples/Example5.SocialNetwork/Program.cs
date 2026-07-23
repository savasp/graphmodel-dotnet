// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;
using Cvoya.Graph.Neo4j;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

// Example 5: Social Network
// Demonstrates a complete social network implementation with users, posts, and interactions

Console.WriteLine("=== Example 5: Social Network ===\n");

const string databaseName = "example5";

// ==== SETUP a new database ====
Console.WriteLine("0. Setting up a new database...");
var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));
await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
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
IGraphQueryable<User> UserSelection(string username) =>
    graph.Nodes<User>().Where(user => user.Username == username);
IGraphQueryable<Post> PostSelection(string content) =>
    graph.Nodes<Post>().Where(post => post.Content == content);
IGraphQueryable<Comment> CommentSelection(string content) =>
    graph.Nodes<Comment>().Where(comment => comment.Content == content);

try
{
    // ==== CREATE USERS ====
    Console.WriteLine("1. Creating social network users...");

    var alice = new User
    {
        Username = "alice_wonder",
        Email = "alice@example.com",
        JoinedDate = DateTime.UtcNow.AddYears(-2),
        Bio = "Software engineer, coffee enthusiast"
    };

    var bob = new User
    {
        Username = "bob_builder",
        Email = "bob@example.com",
        JoinedDate = DateTime.UtcNow.AddYears(-1),
        Bio = "Can we fix it? Yes we can!"
    };

    var charlie = new User
    {
        Username = "charlie_explorer",
        Email = "charlie@example.com",
        JoinedDate = DateTime.UtcNow.AddMonths(-6),
        Bio = "Adventure seeker and photographer"
    };

    var diana = new User
    {
        Username = "diana_artist",
        Email = "diana@example.com",
        JoinedDate = DateTime.UtcNow.AddMonths(-3),
        Bio = "Digital artist and creative soul"
    };

    await graph.CreateNodeAsync(alice);
    await graph.CreateNodeAsync(bob);
    await graph.CreateNodeAsync(charlie);
    await graph.CreateNodeAsync(diana);

    Console.WriteLine($"✓ Created {4} users\n");

    // ==== CREATE FRIENDSHIPS ====
    Console.WriteLine("2. Creating friendships...");

    await graph.CreateRelationshipAsync(
        UserSelection(alice.Username), new Follows { Since = DateTime.UtcNow.AddYears(-1) }, UserSelection(bob.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(bob.Username), new Follows { Since = DateTime.UtcNow.AddYears(-1) }, UserSelection(alice.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(alice.Username), new Follows { Since = DateTime.UtcNow.AddMonths(-6) }, UserSelection(charlie.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(charlie.Username), new Follows { Since = DateTime.UtcNow.AddMonths(-5) }, UserSelection(alice.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(bob.Username), new Follows { Since = DateTime.UtcNow.AddMonths(-4) }, UserSelection(charlie.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(charlie.Username), new Follows { Since = DateTime.UtcNow.AddMonths(-2) }, UserSelection(diana.Username));
    await graph.CreateRelationshipAsync(
        UserSelection(diana.Username), new Follows { Since = DateTime.UtcNow.AddMonths(-2) }, UserSelection(charlie.Username));

    Console.WriteLine("✓ Created friendship connections\n");

    // ==== CREATE POSTS ====
    Console.WriteLine("3. Creating posts...");

    var post1 = new Post
    {
        Content = "Just deployed a new feature using CVOYA Graph! The LINQ support is amazing 🚀",
        PostedAt = DateTime.UtcNow.AddHours(-24),
        Tags = ["programming", "graphdb", "dotnet"]
    };

    var post2 = new Post
    {
        Content = "Building something cool with Neo4j and C#. Graph databases are the future!",
        PostedAt = DateTime.UtcNow.AddHours(-12),
        Tags = ["neo4j", "databases", "csharp"]
    };

    var post3 = new Post
    {
        Content = "Captured an amazing sunset today! Nature never ceases to amaze me 🌅",
        PostedAt = DateTime.UtcNow.AddHours(-6),
        Tags = ["photography", "nature", "sunset"]
    };

    await graph.CreateNodeAsync(post1);
    await graph.CreateNodeAsync(post2);
    await graph.CreateNodeAsync(post3);

    await graph.CreateRelationshipAsync(UserSelection(alice.Username), new Posted(), PostSelection(post1.Content));
    await graph.CreateRelationshipAsync(UserSelection(bob.Username), new Posted(), PostSelection(post2.Content));
    await graph.CreateRelationshipAsync(UserSelection(charlie.Username), new Posted(), PostSelection(post3.Content));

    Console.WriteLine("✓ Created posts\n");

    // ==== CREATE INTERACTIONS ====
    Console.WriteLine("4. Creating interactions...");

    // Likes
    await graph.CreateRelationshipAsync(
        UserSelection(bob.Username), new Likes { LikedAt = DateTime.UtcNow.AddHours(-20) }, PostSelection(post1.Content));
    await graph.CreateRelationshipAsync(
        UserSelection(charlie.Username), new Likes { LikedAt = DateTime.UtcNow.AddHours(-18) }, PostSelection(post1.Content));
    await graph.CreateRelationshipAsync(
        UserSelection(alice.Username), new Likes { LikedAt = DateTime.UtcNow.AddHours(-10) }, PostSelection(post2.Content));
    await graph.CreateRelationshipAsync(
        UserSelection(diana.Username), new Likes { LikedAt = DateTime.UtcNow.AddHours(-4) }, PostSelection(post3.Content));

    // Comments
    var comment1 = new Comment
    {
        Content = "Great work! Can't wait to try it out.",
        CommentedAt = DateTime.UtcNow.AddHours(-19)
    };

    var comment2 = new Comment
    {
        Content = "Thanks! Let me know if you need any help getting started.",
        CommentedAt = DateTime.UtcNow.AddHours(-18)
    };

    await graph.CreateNodeAsync(comment1);
    await graph.CreateNodeAsync(comment2);

    await graph.CreateRelationshipAsync(CommentSelection(comment1.Content), new CommentedOn(), PostSelection(post1.Content));
    await graph.CreateRelationshipAsync(CommentSelection(comment2.Content), new CommentedOn(), PostSelection(post1.Content));
    await graph.CreateRelationshipAsync(
        UserSelection(bob.Username), new Wrote { WrittenAt = DateTime.UtcNow.AddHours(-19) }, CommentSelection(comment1.Content));
    await graph.CreateRelationshipAsync(
        UserSelection(alice.Username), new Wrote { WrittenAt = DateTime.UtcNow.AddHours(-18) }, CommentSelection(comment2.Content));
    await graph.CreateRelationshipAsync(CommentSelection(comment2.Content), new ReplyTo(), CommentSelection(comment1.Content));

    Console.WriteLine("✓ Created likes and comments\n");

    // ==== SOCIAL NETWORK QUERIES ====
    Console.WriteLine("5. Social network analytics...");

    // Find users and their followers
    var usersWithFollowers = await graph.Nodes<User>()
        .PathSegments<User, Follows, User>()
        .ToListAsync();

    var followersCount = usersWithFollowers
        .GroupBy(p => p.EndNode.Username)
        .Select(g => new { Username = g.Key, FollowerCount = g.Count() })
        .OrderByDescending(x => x.FollowerCount)
        .ToList();

    Console.WriteLine("Users by follower count:");
    foreach (var user in followersCount)
    {
        Console.WriteLine($"  - {user.Username}: {user.FollowerCount} followers");
    }

    // Find posts with likes
    var postsWithLikes = await graph.Nodes<User>()
        .PathSegments<User, Likes, Post>()
        .ToListAsync();

    var likeCounts = postsWithLikes
        .GroupBy(p => p.EndNode.Content)
        .Select(g => new { Post = g.First().EndNode, LikeCount = g.Count() })
        .OrderByDescending(x => x.LikeCount)
        .ToList();

    Console.WriteLine("\nPosts by popularity:");
    foreach (var item in likeCounts)
    {
        Console.WriteLine($"  - Post: \"{item.Post.Content.Substring(0, Math.Min(50, item.Post.Content.Length))}...\" ({item.LikeCount} likes)");
    }

    // ==== FEED GENERATION ====
    Console.WriteLine("\n6. Generating personalized feed for Alice...");

    // Get posts from people Alice follows
    var aliceFollowing = await graph.Nodes<User>()
        .Where(u => u.Username == "alice_wonder")
        .PathSegments<User, Follows, User>()
        .ToListAsync();

    var followedUsernames = aliceFollowing.Select(p => p.EndNode.Username).ToHashSet();

    var feedPosts = await graph.Nodes<User>()
        .Where(u => followedUsernames.Contains(u.Username))
        .PathSegments<User, Posted, Post>()
        .ToListAsync();

    Console.WriteLine($"Feed for alice_wonder ({feedPosts.Count} posts):");
    foreach (var path in feedPosts.OrderByDescending(p => p.EndNode.PostedAt).Take(3))
    {
        Console.WriteLine($"  - {path.StartNode.Username} ({path.EndNode.PostedAt:g}):");
        Console.WriteLine($"    \"{path.EndNode.Content}\"");
        Console.WriteLine($"    Tags: {string.Join(", ", path.EndNode.Tags)}");
    }

    // ==== RECOMMENDATION ENGINE ====
    Console.WriteLine("\n7. Friend recommendations for Charlie...");

    // Find friends of friends who Charlie doesn't follow
    var charlieFollowing = await graph.Nodes<User>()
        .Where(u => u.Username == "charlie_explorer")
        .PathSegments<User, Follows, User>()
        .ToListAsync();

    var charlieFollowingUsernames = charlieFollowing.Select(p => p.EndNode.Username).ToHashSet();

    // Find who Charlie's friends are following
    var friendsOfFriends = await graph.Nodes<User>()
        .Where(u => charlieFollowingUsernames.Contains(u.Username))
        .PathSegments<User, Follows, User>()
        .ToListAsync();

    charlie = await graph.Nodes<User>()
        .Where(u => u.Username == "charlie_explorer")
        .FirstOrDefaultAsync();

    if (charlie != null)
    {
        charlieFollowingUsernames.Add(charlie.Username); // Don't recommend self

        var recommendations = friendsOfFriends
            .Where(p => !charlieFollowingUsernames.Contains(p.EndNode.Username))
            .GroupBy(p => p.EndNode.Username)
            .OrderByDescending(g => g.Count()) // More mutual connections = higher score
            .Take(2)
            .Select(g => g.First().EndNode)
            .ToList();

        Console.WriteLine($"Friend recommendations for charlie_explorer:");
        foreach (var user in recommendations)
        {
            var mutualCount = friendsOfFriends
                .Count(p => p.EndNode.Username == user.Username);

            Console.WriteLine($"  - {user.Username} ({mutualCount} mutual connections)");
            Console.WriteLine($"    Bio: {user.Bio}");
        }
    }

    Console.WriteLine("\n=== Example 5 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Building a social network with users, posts, and interactions");
    Console.WriteLine("• Complex relationship patterns (follows, likes, comments)");
    Console.WriteLine("• Social network analytics (follower counts, popular posts)");
    Console.WriteLine("• Feed generation based on social graph");
    Console.WriteLine("• Friend recommendation engine using graph patterns");
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
    await store.DisposeAsync();
    await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
    {
        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}

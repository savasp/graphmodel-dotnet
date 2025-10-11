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

    await graph.CreateRelationshipAsync(new Follows(alice.Id, bob.Id, DateTime.UtcNow.AddYears(-1)));
    await graph.CreateRelationshipAsync(new Follows(bob.Id, alice.Id, DateTime.UtcNow.AddYears(-1)));
    await graph.CreateRelationshipAsync(new Follows(alice.Id, charlie.Id, DateTime.UtcNow.AddMonths(-6)));
    await graph.CreateRelationshipAsync(new Follows(charlie.Id, alice.Id, DateTime.UtcNow.AddMonths(-5)));
    await graph.CreateRelationshipAsync(new Follows(bob.Id, charlie.Id, DateTime.UtcNow.AddMonths(-4)));
    await graph.CreateRelationshipAsync(new Follows(charlie.Id, diana.Id, DateTime.UtcNow.AddMonths(-2)));
    await graph.CreateRelationshipAsync(new Follows(diana.Id, charlie.Id, DateTime.UtcNow.AddMonths(-2)));

    Console.WriteLine("✓ Created friendship connections\n");

    // ==== CREATE POSTS ====
    Console.WriteLine("3. Creating posts...");

    var post1 = new Post
    {
        Content = "Just deployed a new feature using GraphModel! The LINQ support is amazing 🚀",
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

    await graph.CreateRelationshipAsync(new Posted(alice.Id, post1.Id));
    await graph.CreateRelationshipAsync(new Posted(bob.Id, post2.Id));
    await graph.CreateRelationshipAsync(new Posted(charlie.Id, post3.Id));

    Console.WriteLine("✓ Created posts\n");

    // ==== CREATE INTERACTIONS ====
    Console.WriteLine("4. Creating interactions...");

    // Likes
    await graph.CreateRelationshipAsync(new Likes(bob.Id, post1.Id, DateTime.UtcNow.AddHours(-20)));
    await graph.CreateRelationshipAsync(new Likes(charlie.Id, post1.Id, DateTime.UtcNow.AddHours(-18)));
    await graph.CreateRelationshipAsync(new Likes(alice.Id, post2.Id, DateTime.UtcNow.AddHours(-10)));
    await graph.CreateRelationshipAsync(new Likes(diana.Id, post3.Id, DateTime.UtcNow.AddHours(-4)));

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

    await graph.CreateRelationshipAsync(new CommentedOn(comment1.Id, post1.Id));
    await graph.CreateRelationshipAsync(new CommentedOn(comment2.Id, post1.Id));
    await graph.CreateRelationshipAsync(new Wrote(bob.Id, comment1.Id, DateTime.UtcNow.AddHours(-19)));
    await graph.CreateRelationshipAsync(new Wrote(alice.Id, comment2.Id, DateTime.UtcNow.AddHours(-18)));
    await graph.CreateRelationshipAsync(new ReplyTo(comment2.Id, comment1.Id));

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
        .GroupBy(p => p.EndNode.Id)
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

    var followedUserIds = aliceFollowing.Select(p => p.EndNode.Id).ToHashSet();

    var feedPosts = await graph.Nodes<User>()
        .Where(u => followedUserIds.Contains(u.Id))
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

    var charlieFollowingIds = charlieFollowing.Select(p => p.EndNode.Id).ToHashSet();

    // Find who Charlie's friends are following
    var friendsOfFriends = await graph.Nodes<User>()
        .Where(u => charlieFollowingIds.Contains(u.Id))
        .PathSegments<User, Follows, User>()
        .ToListAsync();

    charlie = await graph.Nodes<User>()
        .Where(u => u.Username == "charlie_explorer")
        .FirstOrDefaultAsync();

    if (charlie != null)
    {
        charlieFollowingIds.Add(charlie.Id); // Don't recommend self

        var recommendations = friendsOfFriends
            .Where(p => !charlieFollowingIds.Contains(p.EndNode.Id))
            .GroupBy(p => p.EndNode.Id)
            .OrderByDescending(g => g.Count()) // More mutual connections = higher score
            .Take(2)
            .Select(g => g.First().EndNode)
            .ToList();

        Console.WriteLine($"Friend recommendations for charlie_explorer:");
        foreach (var user in recommendations)
        {
            var mutualCount = friendsOfFriends
                .Count(p => p.EndNode.Id == user.Id);

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
    await graph.DisposeAsync();
    await using (var session = driver.AsyncSession(sc => sc.WithDatabase("system")))
    {
        await session.RunAsync($"DROP DATABASE {databaseName}");
    }
    await driver.DisposeAsync();
}
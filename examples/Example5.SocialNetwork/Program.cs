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
using Cvoya.Graph.Provider.Neo4j;
using Neo4j.Driver;

// Example 5: Social Network
// Demonstrates a complete social network implementation with users, posts, and interactions


/* This example still has some issues with featurs that haven't been implemented properly.

Console.WriteLine("=== Example 5: Social Network ===\n");

const string databaseName = "example5";

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

    await graph.CreateNode(alice);
    await graph.CreateNode(bob);
    await graph.CreateNode(charlie);
    await graph.CreateNode(diana);

    Console.WriteLine($"✓ Created {4} users\n");

    // ==== CREATE FRIENDSHIPS ====
    Console.WriteLine("2. Creating friendships...");

    await graph.CreateRelationship(new Follows { Source = alice, Target = bob, Since = DateTime.UtcNow.AddYears(-1) });
    await graph.CreateRelationship(new Follows { Source = bob, Target = alice, Since = DateTime.UtcNow.AddYears(-1) });
    await graph.CreateRelationship(new Follows { Source = alice, Target = charlie, Since = DateTime.UtcNow.AddMonths(-6) });
    await graph.CreateRelationship(new Follows { Source = charlie, Target = alice, Since = DateTime.UtcNow.AddMonths(-5) });
    await graph.CreateRelationship(new Follows { Source = bob, Target = charlie, Since = DateTime.UtcNow.AddMonths(-4) });
    await graph.CreateRelationship(new Follows { Source = charlie, Target = diana, Since = DateTime.UtcNow.AddMonths(-2) });
    await graph.CreateRelationship(new Follows { Source = diana, Target = charlie, Since = DateTime.UtcNow.AddMonths(-2) });

    Console.WriteLine("✓ Created friendship connections\n");

    // ==== CREATE POSTS ====
    Console.WriteLine("3. Creating posts...");

    var post1 = new Post
    {
        Author = new Author { Target = alice },
        Content = "Just deployed a new feature using GraphModel! The LINQ support is amazing 🚀",
        PostedAt = DateTime.UtcNow.AddHours(-24),
        Tags = ["programming", "graphdb", "dotnet"]
    };

    var post2 = new Post
    {
        Author = new Author { Target = bob },
        Content = "Building something cool with Neo4j and C#. Graph databases are the future!",
        PostedAt = DateTime.UtcNow.AddHours(-12),
        Tags = ["neo4j", "databases", "csharp"]
    };

    var post3 = new Post
    {
        Author = new Author { Target = charlie },
        Content = "Captured an amazing sunset today! Nature never ceases to amaze me 🌅",
        PostedAt = DateTime.UtcNow.AddHours(-6),
        Tags = ["photography", "nature", "sunset"]
    };

    await graph.CreateNode(post1);
    await graph.CreateNode(post2);
    await graph.CreateNode(post3);

    await graph.CreateRelationship(new Posted { Source = alice, Target = post1 });
    await graph.CreateRelationship(new Posted { Source = bob, Target = post2 });
    await graph.CreateRelationship(new Posted { Source = charlie, Target = post3 });

    Console.WriteLine("✓ Created posts\n");

    // ==== CREATE INTERACTIONS ====
    Console.WriteLine("4. Creating interactions...");

    // Likes
    await graph.CreateRelationship(new Likes { Source = bob, Target = post1, LikedAt = DateTime.UtcNow.AddHours(-20) });
    await graph.CreateRelationship(new Likes { Source = charlie, Target = post1, LikedAt = DateTime.UtcNow.AddHours(-18) });
    await graph.CreateRelationship(new Likes { Source = alice, Target = post2, LikedAt = DateTime.UtcNow.AddHours(-10) });
    await graph.CreateRelationship(new Likes { Source = diana, Target = post3, LikedAt = DateTime.UtcNow.AddHours(-4) });

    // Comments
    var comment1 = new Comment
    {
        Author = new Author { Target = bob },
        Content = "Great work! Can't wait to try it out.",
        CommentedAt = DateTime.UtcNow.AddHours(-19)
    };

    var comment2 = new Comment
    {
        Author = new Author { Target = alice },
        Content = "Thanks! Let me know if you need any help getting started.",
        CommentedAt = DateTime.UtcNow.AddHours(-18),
        ReplyTo = new ReplyTo { Target = comment1 }
    };

    await graph.CreateNode(comment1);
    await graph.CreateNode(comment2);

    await graph.CreateRelationship(new CommentedOn { Source = comment1, Target = post1 });
    await graph.CreateRelationship(new CommentedOn { Source = comment2, Target = post1 });
    await graph.CreateRelationship(new Wrote { Source = bob, Target = comment1 });
    await graph.CreateRelationship(new Wrote { Source = alice, Target = comment2 });

    Console.WriteLine("✓ Created likes and comments\n");

    // ==== SOCIAL NETWORK QUERIES ====
    Console.WriteLine("5. Social network analytics...");

    // Find mutual followers
    var aliceFollowers = graph.Nodes<User>(new GraphOperationOptions().WithDepth(1))
        .Where(u => u.Username == "alice_wonder")
        .SelectMany(u => u.Followers.Select(f => f.Source))
        .ToList();

    var bobFollowers = graph.Nodes<User>(new GraphOperationOptions().WithDepth(1))
        .Where(u => u.Username == "bob_builder")
        .SelectMany(u => u.Followers.Select(f => f.Source))
        .ToList();

    var mutualFollowers = aliceFollowers.Intersect(bobFollowers).ToList();
    Console.WriteLine($"Mutual followers of Alice and Bob: {mutualFollowers.Count}");

    // Find most liked posts
    var postsWithLikes = graph.Nodes<Post>(new GraphOperationOptions().WithDepth(2))
        .Select(p => new { Post = p, LikeCount = p.LikedBy.Count() })
        .OrderByDescending(p => p.LikeCount)
        .ToList();

    Console.WriteLine("\nPosts by popularity:");
    foreach (var item in postsWithLikes)
    {
        Console.WriteLine($"  - Post by {item.Post.Author!.Target!.Username}: {item.LikeCount} likes");
        Console.WriteLine($"    \"{item.Post.Content.Substring(0, Math.Min(50, item.Post.Content.Length))}...\"");
    }

    // Find users who follow each other (mutual following)
    var mutualFollows = graph.Nodes<User>(new GraphOperationOptions().WithDepth(1))
        .SelectMany(u => u.Following
            .Where(f => f.Target != null && f.Target.Following.Any(f2 => f2.Target!.Id == u.Id))
            .Select(f => new { User1 = u.Username, User2 = f.Target!.Username }))
        .Where(pair => string.Compare(pair.User1, pair.User2) < 0) // Avoid duplicates
        .ToList();

    Console.WriteLine("\nMutual connections:");
    foreach (var pair in mutualFollows)
    {
        Console.WriteLine($"  - {pair.User1} ↔ {pair.User2}");
    }

    // ==== FEED GENERATION ====
    Console.WriteLine("\n6. Generating personalized feed for Alice...");

    var aliceUser = graph.Nodes<User>(new GraphOperationOptions().WithDepth(2))
        .FirstOrDefault(u => u.Username == "alice_wonder");

    if (aliceUser != null)
    {
        // Get posts from people Alice follows
        var feedPosts = aliceUser.Following
            .SelectMany(f => f.Target?.Posts ?? Enumerable.Empty<Posted>())
            .Select(p => p.Target)
            .Where(p => p != null)
            .OrderByDescending(p => p!.PostedAt)
            .Take(10)
            .ToList();

        Console.WriteLine($"Feed for {aliceUser.Username}:");
        foreach (var post in feedPosts)
        {
            if (post != null)
            {
                var likes = graph.Relationships<Likes>(new GraphOperationOptions().WithDepth(0))
                    .Count(l => l.Target!.Id == post.Id);

                Console.WriteLine($"  - {post.Author!.Target!.Username} ({post.PostedAt:g}):");
                Console.WriteLine($"    \"{post.Content}\"");
                Console.WriteLine($"    {likes} likes, Tags: {string.Join(", ", post.Tags)}");
            }
        }
    }

    // ==== RECOMMENDATION ENGINE ====
    Console.WriteLine("\n7. Friend recommendations for Charlie...");

    var charlieUser = graph.Nodes<User>(new GraphOperationOptions().WithDepth(2))
        .FirstOrDefault(u => u.Username == "charlie_explorer");

    if (charlieUser != null)
    {
        // Find friends of friends who Charlie doesn't follow
        var currentFollowing = charlieUser.Following.Select(f => f.Target?.Id).ToHashSet();
        currentFollowing.Add(charlieUser.Id); // Don't recommend self

        var recommendations = charlieUser.Following
            .SelectMany(f => f.Target?.Following ?? Enumerable.Empty<Follows>())
            .Select(f => f.Target)
            .Where(u => u != null && !currentFollowing.Contains(u.Id))
            .GroupBy(u => u!.Id)
            .OrderByDescending(g => g.Count()) // More mutual connections = higher score
            .Take(3)
            .Select(g => g.First())
            .ToList();

        Console.WriteLine($"Friend recommendations for {charlieUser.Username}:");
        foreach (var user in recommendations)
        {
            if (user != null)
            {
                var mutualCount = charlieUser.Following
                    .Count(f => f.Target?.Following.Any(f2 => f2.Target?.Id == user.Id) ?? false);

                Console.WriteLine($"  - {user.Username} ({mutualCount} mutual connections)");
                Console.WriteLine($"    Bio: {user.Bio}");
            }
        }
    }

    Console.WriteLine("\n=== Example 5 Complete ===");
    Console.WriteLine("This example demonstrated:");
    Console.WriteLine("• Building a social network with users, posts, and interactions");
    Console.WriteLine("• Complex relationship patterns (follows, likes, comments)");
    Console.WriteLine("• Social network analytics (mutual connections, popularity)");
    Console.WriteLine("• Feed generation based on social graph");
    Console.WriteLine("• Friend recommendation engine");
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
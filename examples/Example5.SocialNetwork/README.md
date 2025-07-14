# Example 5: Social Network

This example demonstrates a complete social network implementation using the GraphModel library, showcasing real-world relationship patterns and graph analytics.

## What You'll Learn

- Building a social network graph structure
- Managing complex relationships (follows, likes, comments)
- Social network analytics and metrics  
- Feed generation based on social graph
- Friend recommendation using graph patterns

## Features Demonstrated

### 1. User Management

```csharp
[Node(Label = "User")]
public record User : Node
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public string? Bio { get; set; } = string.Empty;
}
```

### 2. Content Creation

Posts with tags, timestamps, and author relationships:

```csharp
[Node(Label = "Post")]
public record Post : Node
{
    public DateTime PostedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
```

### 3. Social Interactions

Multiple relationship types for different interactions:

- **FOLLOWS**: User following relationships with timestamps
- **POSTED**: User to post authorship
- **LIKES**: User likes on posts with timestamps  
- **COMMENTED_ON**: Comments on posts
- **WROTE**: User authorship of comments

### 4. Analytics Queries

Current implementation demonstrates:

- Follower count analysis
- Post popularity metrics  
- Mutual connection discovery
- Engagement tracking

### 5. Feed Generation

Personalized feed based on who the user follows:

```csharp
// Get posts from people Alice follows
var aliceFollowing = await graph.Nodes<User>()
    .Where(u => u.Username == "alice_wonder")
    .PathSegments<User, Follows, User>()
    .ToListAsync();

var feedPosts = await graph.Nodes<User>()
    .Where(u => followedUserIds.Contains(u.Id))
    .PathSegments<User, Posted, Post>()
    .ToListAsync();
```

### 6. Friend Recommendations

Suggest new connections based on:

- Friends of friends patterns
- Mutual connection count
- Graph traversal algorithms

## Social Network Patterns

The example demonstrates common social network patterns:

- Bidirectional relationships (mutual following)
- Content engagement tracking
- Social graph traversal and analytics
- Basic recommendation algorithms

## Running the Example

**Note: This example requires .NET 10.0 which is not yet released.**

```bash
cd examples/Example5.SocialNetwork
dotnet run
```

Make sure Neo4j is running and accessible at `bolt://localhost:7687` with username `neo4j` and password `password`.

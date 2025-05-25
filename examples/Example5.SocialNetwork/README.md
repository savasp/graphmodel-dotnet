# Example 5: Social Network

This example demonstrates a complete social network implementation using the GraphModel library, showcasing real-world patterns and queries.

## What You'll Learn

- Building a social network graph structure
- Managing complex relationships (follows, likes, comments)
- Social network analytics and metrics
- Feed generation algorithms
- Friend recommendation systems

## Features Demonstrated

### 1. User Management

```csharp
[Node("User")]
public class User : Node
{
    public string Username { get; set; }
    public string Email { get; set; }
    public DateTime JoinedDate { get; set; }
    public string Bio { get; set; }
}
```

### 2. Content Creation

Posts with tags, timestamps, and author relationships:

```csharp
[Node("Post")]
public class Post : Node
{
    public User? Author { get; set; }
    public string Content { get; set; }
    public DateTime PostedAt { get; set; }
    public List<string> Tags { get; set; }
}
```

### 3. Social Interactions

Multiple relationship types for different interactions:

- **FOLLOWS**: User following relationships
- **POSTED**: User to post authorship
- **LIKES**: User likes on posts
- **COMMENTED_ON**: Comments on posts

### 4. Analytics Queries

- Find mutual followers
- Calculate post popularity
- Identify mutual connections
- Generate engagement metrics

### 5. Feed Generation

Personalized feed based on who the user follows:

```csharp
var feedPosts = user.Following
    .SelectMany(f => f.Target?.Posts)
    .OrderByDescending(p => p.PostedAt)
    .Take(10);
```

### 6. Friend Recommendations

Suggest new connections based on:

- Friends of friends
- Mutual connection count
- Shared interests (via tags)

## Social Network Patterns

The example demonstrates common social network patterns:

- Bidirectional relationships (mutual following)
- Content engagement (likes and comments)
- Social graph traversal
- Recommendation algorithms

## Running the Example

```bash
cd examples/Example5.SocialNetwork
dotnet run
```

Make sure Neo4j is running and accessible at `neo4j://localhost:7687`.

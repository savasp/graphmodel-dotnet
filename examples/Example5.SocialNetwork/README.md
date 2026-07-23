# Example 5: Social network

This .NET 10 example builds a social graph with users, posts, comments, follows, likes, and
authorship relationships. It demonstrates path-based analytics, feed generation, and friend
recommendations without relying on public provider identity.

Endpoint queries use ordinary domain properties such as `Username` and `Content`:

```csharp
IGraphQueryable<User> UserSelection(string username) =>
    graph.Nodes<User>().Where(user => user.Username == username);

await graph.CreateRelationshipAsync(
    UserSelection("alice_wonder"),
    new Follows { Since = DateTime.UtcNow.AddYears(-1) },
    UserSelection("bob_builder"));
```

Path segments expose the selected users, relationship value, and edge orientation:

```csharp
var aliceFollowing = await graph.Nodes<User>()
    .Where(user => user.Username == "alice_wonder")
    .PathSegments<User, Follows, User>()
    .ToListAsync();

var followedUsernames = aliceFollowing
    .Select(segment => segment.EndNode.Username)
    .ToHashSet();

var feedPosts = await graph.Nodes<User>()
    .Where(user => followedUsernames.Contains(user.Username))
    .PathSegments<User, Posted, Post>()
    .ToListAsync();
```

`User`, `Post`, and each relationship type are valid keyless entities. Applications can add
explicit `[Property(IsKey = true)]` members when they want provider-enforced domain uniqueness.

## Run

Start Neo4j at `bolt://localhost:7687` with username `neo4j` and password `password`, then run:

```bash
dotnet run --project examples/Example5.SocialNetwork
```

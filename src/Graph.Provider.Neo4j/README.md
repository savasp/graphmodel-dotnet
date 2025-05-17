# Neo4j LINQ Query Examples

Below are some example LINQ queries supported by the Neo4j client, including navigational queries that hydrate related nodes and relationships.

## 1. Simple Node Query

```csharp
// Get all users with a specific name
var users = client.Nodes<User>().Where(u => u.Name == "Alice").ToList();
```

## 2. Projection

```csharp
// Get just the names of all users
var names = client.Nodes<User>().Select(u => u.Name).ToList();
```

## 3. Sorting and Limiting

```csharp
// Get the 10 youngest users
var youngest = client.Nodes<User>().OrderBy(u => u.Age).Take(10).ToList();
```

## 4. Navigational Query: Hydrate Relationships

Suppose your `User` class has a property:

```csharp
public List<Friendship> Friendships { get; set; }
```

where `Friendship` implements `IRelationship<User, User>`.

```csharp
// Get Alice and her friendships (and friends), up to 2 hops deep
var aliceWithFriends = (await client.Nodes<User>()
    .Where(u => u.Name == "Alice")
    .ToListAsync(traversalDepth: 2)).FirstOrDefault();

// aliceWithFriends.Friendships will be populated
// Each Friendship will have Source and Target User hydrated (up to depth 2)
```

## 5. Navigational Query: Traverse to Related Nodes

Suppose you want to get all users and their posts (where `Post` is another node type and `Authored` is a relationship):

```csharp
public class User : INode
{
    public List<Authored> AuthoredPosts { get; set; }
}
public class Authored : IRelationship<User, Post>
{
    public Post Target { get; set; }
}
public class Post : INode
{
    public string Title { get; set; }
}

// Query and hydrate authored posts for each user
var usersWithPosts = await client.Nodes<User>().ToListAsync(traversalDepth: 2);
// For each user, user.AuthoredPosts will be hydrated,
// and each Authored.Target (the Post) will also be hydrated.
```

## 6. Deep Navigational Query

```csharp
// Get users, their friends, and their friends' posts (3 levels deep)
var deep = await client.Nodes<User>().ToListAsync(traversalDepth: 3);
```

## 7. Anonymous Projection with Navigation

```csharp
// Get user names and their friends' names
var userFriends = client.Nodes<User>()
    .Select(u => new {
        u.Name,
        Friends = u.Friendships.Select(f => f.Target.Name)
    })
    .ToList();
```

---

**Note:**

- The actual navigation and hydration depend on your model structure and the traversal depth you specify.
- For advanced navigational queries, always set an appropriate `traversalDepth` to avoid infinite recursion in cyclic graphs.

## Neo4j Graph Client

The Neo4j Graph Client is an implementation of the `IGraphProvider` interface that provides a way to interact with a Neo4j graph database.

## Usage Example

```csharp
// Create the Neo4j client
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Neo4jGraphProvider>();
var client = new Neo4jGraphProvider("bolt://localhost:7687", "neo4j", "password", logger);

// Create a node
var person = new Person
{
    Id = "john",
    FirstName = "John",
    LastName = "Doe",
    Address = new Address
    {
        Street = "123 Main St",
        City = "Seattle",
        State = "WA",
        Zip = "98101",
        Country = "USA"
    }
};

// Begin a transaction
using var transaction = await client.BeginTransactionAsync();
try
{
    // Create the node
    var createdPerson = await client.CreateNode(person, transaction);

    // Create a relationship
    var relationship = new Knows
    {
        Since = DateTime.Now,
    };

    var friend = new Person
    {
        Id = "jane",
        FirstName = "Jane",
        LastName = "Smith"
    };

    // Create the friend node
    var createdFriend = await client.CreateNode(friend, transaction);

    // Create the relationship between the two nodes
    var createdRelationship = await client.CreateRelationshipAsync(relationship, createdPerson, createdFriend, transaction);

    // Query nodes
    var query = client.Nodes<Person>(transaction)
        .Where(p => p.FirstName == "John")
        .ToList();

    // Update a node
    createdPerson.LastName = "Johnson";
    var updatedPerson = await client.UpdateNodeAsync(createdPerson, transaction);

    // Update a relationship
    createdRelationship.Since = DateTime.Now.AddDays(-1);
    var updatedRelationship = await client.UpdateRelationshipAsync(createdRelationship, transaction);

    // Delete a relationship
    await client.DeleteRelationshipAsync(createdRelationship.Id, transaction);

    // Delete a node
    await client.DeleteNodeAsync(createdFriend.Id, transaction);

    // Commit the transaction
    await transaction.CommitAsync();
}
catch (Exception)
{
    // Rollback the transaction
    await transaction.RollbackAsync();
    throw;
}
```

## Features

The Neo4j Graph Client provides the following features:

- CRUD operations for nodes and relationships
- Transaction support
- LINQ query support
- Support for complex object graphs

## Implementation Details

The Neo4j Graph Client uses the official Neo4j .NET driver to interact with the database. It maps between .NET objects and Neo4j nodes and relationships using the following conventions:

- C# class names are used as Neo4j labels
- C# properties are mapped to Neo4j properties
- Relationship types are derived from the relationship class name

The client handles the serialization of complex types to and from Neo4j properties.

## Error Handling

The client throws appropriate exceptions for various error conditions:

- `ArgumentNullException` for null arguments
- `InvalidOperationException` for logical errors
- `GraphProviderException` for Neo4j driver errors

## Performance Considerations

For best performance, consider the following:

- Use transactions for batching operations
- Limit the depth of object graphs to avoid serialization overhead
- Create indexes on properties that are frequently used in queries

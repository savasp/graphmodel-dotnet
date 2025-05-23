# Graph Model

The Graph Model provides a set of interfaces and abstractions for interacting with graphs. Implementations of the interface might or might not be backed by a (graph) database.

## Core Interfaces

### IGraph

The `IGraph` interface is the main entry point for interacting with a graph. It provides methods for:

- Creating, reading, updating, and deleting nodes and relationships
- Querying nodes and relationships
- Managing transactions

### IEntity

The `IEntity` interface represents a uniquely identifiable entity in the graph such as a node or a relationship. Middleware builders implement this interface indirectly via one of the derived interfaces.

```csharp
public interface IEntity
{
    string Id { get; }
}
```

### `INode`

The `INode` interface represents a node in the graph. All classes that should be serialized as graph nodes should implement this interface directly.

```csharp
public interface INode : IEntity
{
}
```

### `IRelationship`

The `IRelationship` interface represents a relationship in the graph. Classes that represent relationships with properties in the graph must implement this interface.

````csharp
public interface IRelationship : IEntity
{
    string SourceId { get; }
    string TargetId { get; }
}

### `IRelationship<TSource, TTarget>`

The `IRelationship` interface represents a relationship between two nodes in the graph. This is used for navigation queries.

```csharp
public interface IRelationship<TSource, TTarget> : IRelationship
    where TSource : INode
    where TTarget : INode
{
    TSource Source { get; }
    TTarget Target { get; }
}
````

### IGraphTransaction

The `IGraphTransaction` interface represents a transaction in the graph database. It provides methods for committing and rolling back transactions.

```csharp
public interface IGraphTransaction : IAsyncDisposable
{
    Task CommitAsync();

    Task RollbackAsync();
}
```

## Exception Handling

The model defines two custom exception types:

- `GraphException`: Thrown when an operation on the graph client fails
- `GraphTransactionException`: Thrown when a transaction operation fails

## Attributes

Attributes can help application middleware builders to customize aspects of a graph's representation.

### NodeAttribute

When used to annotate a class that implements (directly or indirectly) the `INode` interface, it specifies the label that should be used in the graph database for nodes of that type. If not specified, the name of the class will be used instead.

### RelationshipAttribute

When used to annotate a class that implements (directly or indirectly) the `IRelationship` interface, it specifies the label that should be used in the graph database for relationships of that type. If not specified, the name of the class will be used instead.

### PropertyAttribute

Some graph databases might not support node or relationship properties of non-primitive types. It is expected that graph store providers will automatically convert such properties to graph nodes. The name of the property is used to identify the relationship (i.e., the label) in the graph database. The `PropertyAttribute` can be used to specify an alternative name.

## Representation of objects in the graph store

### `INode` properties

An `INode` instance represents a graph node. The properties of the `INode` represent the properties of that graph node. The following types are supported as `INode` properties:

- "Primitive types": All .NET value types and strings. Examples: int, bool, DateTime, enums, double, float, string. The library provides a `Point` struct that is also considered a primitive type. .NET's `TimeSpan` is NOT a supported primitive type.
- "Complex types": Classes, records, or structs. Their instances must not lead to reference cycles. Graph providers (e.g. Neo4j) that don't support complex type properties must recursively serialize them as separate graph nodes and establish a relationship using the property name as a label. These graph nodes are not addressable. They cannot participate in relationships. Their lifetime is tightly-coupled with that of the parent graph node.
  `IRelationship<TSource, TTarget>`: Represents a relationship between two graph nodes.
- Collections: `IEnumerable`, `ICollection`, `ISet`, and arrays of the above types. For ordered collections (e.g. `IList`), the graph provider must capture the order in its internal representation. If the generic type argument is a complex type other than `IRelationship`, then providers that don't support properties of complex types will have to recursively serialize them as graph nodes as per "complex types" above.

> **Note**: The graph node's label is that of the namespace-qualified name of the class with all the "."s replaced by "\_"s or the one defined by the `NodeProperty`.
>
> **Note**: `INode` cannot be the type of a property.

### `IRelationship` properties

An `IRelationship<TSource, TTarget>` instance represents an edge between two graph nodes. The `IRelationship<TSource, TTarget>` represent the properties of that relationship. The following types are supported as `IRelationship<TSource, TTarget>` properties:

- "Primitives types": All .NET value types and strings. Examples: int, bool, DateTime, enums, double, float, string.
- Collections: `IEnumerable`, `ICollection`, `ISet`, and arrays of the above types.
- The relationship's label is that of the namespace-qualified name of the class with all the "."s replaced by "\_"s.

**Classes implementing the `IRelationship` interface cannot have properties of any other type.**

> **Note:** This automatic conversion of complex properties to nodes and relationships is only expected for `INode` instances. `IRelationship` implementations must not have complex properties. If a relationship class contains a property of a non-primitive type or of a collection of primitive types, the graph provider should throw an exception.

### Examples

Consider the following example:

```csharp
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}

public class Person : INode
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    [PropertyAttribute("HAS_ADDRESS")]
    public Address Address { get; set; }
}
```

In the above example, the `Address` property is converted to a graph node with the label `HAS_ADDRESS`. These automatically-generated relationships cannot be represented as separate in-memory `IRelationship` instances.

If the property type implements (directly or indirectly) the `IRelationship` interface, then the `PropertyAttribute` has no effect since the label of the graph relationship is that of the property type.

The following example represents the person's address through a separation relationship.

```csharp
public class Address : INode
{
    public string Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}

[Relationship("HAS_ADDRESS")]
public class HasAddress : IRelationship<Person, Address>
{
    public string Id { get; set; }
    public Person Source { get; set; }
    public Address Target { get; set; }
}

public class Person : INode
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    public HasAddress Address { get; set; }
}
```

## Dealing with class hierarchies

Each graph and relationship added to the graph via a provider is associated with the label of the instance, not the type used to add the instance to the provider. When getting nodes and relationships from a provider, the label is used to figure out which type in the type hierarchy should be used. Consider the following example:

```csharp
public class Person : INode
{
    public string Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class Manager : Person
{
    public string Department { get; set; }
}

/// snip

Person person = new Manager { Id = "1234", ... }
await provider.Create(person)
```

Even though the type of the added node is `Person`, the instance being added is of type `Manager`. The label for this node should be the namespace-qualified name (or the one provided via the `NodeAttribute`) of the `Manager` class.

The same is true for the representation of `IRelationship` instances.

The above does not mean that there is tight-coupling between an application's class hierarchy and the graph representation. Different .NET classes may be used to represent the same information in the graph. The serialization/deserialization should continue to work as long as the .NET classes map to the same graph nodes and relationships. The following should work just fine:

Now, consider the follow up example below:

```csharp
namespace Namespace1 {
    [Node("Graph_Person")]
    public class Person : INode
    {
        public string Id { get; set; }

        [Property("first")]
        public string FirstName { get; set; }

        [Property("last")]
        public string LastName { get; set; }
    }

    [Node("Graph_Manager")]
    public class Manager : Person
    {
        [Property("department")]
        public string Department { get; set; }
    }
}

namespace Namespace2 {
    [Node("Graph_Person")]
    public class Someone : INode
    {
        public string Id { get; set; }

        [Property("first")]
        public string FirstName { get; set; }

        [Property("first")]
        public string LastName { get; set; }
    }

    [Node("Graph_Manager")]
    public class SomeoneWhoIsManager : Someone
    {
        [Property("department")]
        public string Department { get; set; }
    }
}

/// ...

var person1 = new Namespace1.Manager { Id = "1234", ... }
await provider.Create(person1);

// This should work just fine
var person2 = await provider.Get<Namespace2.Someone>("1234");

```

The label of the graph node with Id "1234" is `Graph_Manager`. The provider checks the type hierarchy of the class `Namespace2.Someone` and identifies the class `SomeoneWhoIsManager` which has the label `GraphManager`. The provider then deserializes node "1234" to an instance of `SomeoneWhoIsManager`.

## LINQ queries

Below are some example LINQ queries that graph store providers must support, including navigational queries that hydrate related nodes and relationships.

### Simple Node Query

```csharp
// Get all users with a specific name
var users = client.Nodes<User>().Where(u => u.Name == "Alice").ToList();
```

### Projection

```csharp
// Get just the names of all users
var names = client.Nodes<User>().Select(u => u.Name).ToList();
```

### Sorting and Limiting

```csharp
// Get the 10 youngest users
var youngest = client.Nodes<User>().OrderBy(u => u.Age).Take(10).ToList();
```

### Navigational Query: Hydrate Relationships

Suppose the `User` class has a property:

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

### Navigational Query: Traverse to Related Nodes

Suppose you want to get all users and their posts (where `Post` is another node type and `Authored` is a relationship):

```csharp
public class User : INode
{
    public List<Authored> AuthoredPosts { get; set; }
}
public class Authored : IRelationship<User, Post>
{
    public User Source { get; set; }
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

### Deep Navigational Query

```csharp
// Get users, their friends, and their friends' posts (3 levels deep)
var deep = await client.Nodes<User>().ToListAsync(traversalDepth: 3);
```

### Anonymous Projection with Navigation

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

- The actual navigation and hydration depend on the application's model structure and the traversal depth you specify.
- For advanced navigational queries, always set an appropriate `traversalDepth` to avoid infinite recursion in cyclic graphs.

## Usage Example

```csharp
var client = new SomeGraphProvider();

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
        Source = createdPerson,
        Target = new Person
        {
            Id = "jane",
            FirstName = "Jane",
            LastName = "Smith"
        },
        Since = DateTime.Now,
    };

    // Create the relationship between the two nodes
    var createdRelationship = await client.CreateRelationship(relationship, transaction);

    // Query nodes
    var query = client.Nodes<Person>(transaction)
        .Where(p => p.FirstName == "John")
        .ToList();

    // Update a node
    createdPerson.LastName = "Johnson";
    var updatedPerson = await client.UpdateNode(createdPerson, transaction);

    // Update a relationship
    createdRelationship.Since = DateTime.Now.AddDays(-1);
    var updatedRelationship = await client.UpdateRelationship(createdRelationship, transaction);

    // Delete a relationship
    await client.DeleteRelationship(createdRelationship.Id, transaction);

    // Delete a node
    await client.DeleteNode(createdRelationship.Target.Id, transaction);

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

# Reimplement the neo4j provider.

The `Graph.Model` abstraction has been updated. The `Graph.Provider.Neo4j` implements the old version of the abstraction.

- Create a new .NET10 project called "Graph.Provider.Neo4j" in the folder `src/Graph.Provider.Neo4j2`. Use the namespace `Cvoya.Graph.Provider.Neo4j`.
- The main interface to implement is `Graph.Model.IGraph` from the `src/Graph.Model` interface.
- Use the documentation in `src/Graph.Model` as guidance for the desired behavior.
- The existing implementation in `src/Graph.Provider.Neo4j` has a lot of code that you can reuse. As you work on the new implementation, reuse any of the existing code as it makes sense.
- Create a new test project in the "tests" folder for this new project.

Pay attention to how `INode` instances are serialized/deserialized in the existing code. Consider this example:

```
public class Person : Node
{
    public string FirstName { get; set; } = string.Empty();
    public Address Home { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = string.Empty();
}

var person = new Person { FirstName = "Savas", Home = new Address { Street = "foo bar" } };
await graph.CreateNode(person);
```

The `Home` property is considered a "complex property". As such, it is serialized as a separate neo4j graph node. The relationship between the person neo4j graph node and the address is set to `__PROPERTY__Home__`. These relationships are considered private and shouldn't be visible through the query layer. In fact, they don't have an `Id` property like `Cvoya.Graph.Model.INode` and `Cvoya.Graph.Model.IRelationship`.

The existing code already has implementation for how to serialize and deserialize complex in-memory graphs. Look at `Neo4jNodeManager.GetNode()` and `Neo4jNodeManager.CreateNode()`.

Classes implemented `INode` and `IRelationship` cannot have properties of `INode` or `IRelationship`.

The existing code implements more constraints. Here are just some examples. Look in the code for more.

- .NET's `Timestamp` isn't supported as an `INode` or `IRelationship` property type.
- Arrays and lists of "simple" properties are supported.
- Arrays and lists of "complex" properties are also supported. They are serialized as multiple relationships between the parent node and purposely-created neo4j graph nodes as per the description above.
- Complex in-memory graphs are supported, even with sharing of objects, as long as there is no reference cycle.

Ask clarifying questions if you need to. Come back to this document between your iterations.

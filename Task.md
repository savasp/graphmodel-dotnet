The Neo4j provider doesn't implement the behavior that the newly added ClassHierarchyTestsBase tests expect. The issue has to do with the label and type we use when adding a node and a relationship respectively. Instead of using the one associated with the type of the variable used to add a node/relationship, we should be using the type of the actual object.

When deserializing a node or relationship from the database, we should use the label/type to go up the class hierarchy of the generic type given in one of the GetNode/GetRelationship operations.

For example...

class Manager : Person { ... }

Person manager = new Manager { Id = "1", ... }

// The type of the variable is Person but the Label should be "Manager"
await graph.CreateNode(manager)

// We are asking to retrieve a Person but the returned object should be a manager
var person = await graph.GetNode<Person>("1")

// This should be true
typeof(person) == typeof(Manager)
Similarly for Relationships

This behavior shoudld work even for this case...

class Person : INode { ... }

[Node(Label="manager")]
class Manager: Person { ... }

var manager = new Manager { Id = "2", ... }

await graph.CreateNode(manager)

class Foo : INode

[Node(Label="manager")]
class Bar : Foo

var bar = await graph.GetNode<Foo>("2")

// this is true
typeof(bar) == typeof(Bar)
The deserialization logic goes up the hierarchy of the requested generic type (ie "Foo") until the neo4j node's label is matched with the Node(Label) value or the class name.

The behavior for relationships should be similar.

For this work, use the savasp/refactor branch as a starting point.

The current Neo4jGraphProvider implementation of the Graph.Model.IGraph interface provides support for serialization/deserialization of in-memory objects to a neo4j graph.

After a refactor, the basic CRUD operations have been re-implemented. Now, we need to reimplement the LINQ query support. The work is done on the `origin/savasp/refactor` branch. That's the starting point, not `origin/main`.

The existing implementation focuses on providing support for the core LINQ operators. In the new implementation, we should introduce graph-specific operations.

In the existing implementation, there was an assumption that application domain classes representing nodes and relationships will have navigation properties (`INode` or `IRelationship`). The implementation after the refactoring doesn't support such navigation properties. We will have to find a way to make LINQ graph queries intuitive and type safe without having to explicitly declare navigation properties into the domain specific classes.

Also, for serialization/deserialization of in-memory objects with complex properties (properties that are data structures), we are now using neo4j relationships that should not be exposed through the LINQ queries. The type of these neo4j relationships starts with `"__PROPERTY__"`.

Example:

```
public class Person
{
  public string FirstName { get; set; }
  public Address Home { get; set; }
}
```

The `Address` property is of type `Address`. The address is represented in Neo4j as a separate node. Instances of Person are connected to an address via the `__PROPERTY__Home__` neo4j relationship. When supporting LINQ queries, however, that representation detail shouldn't be visible.

As you write code, follow these principles:

- Readable, modular, reusable code
- Use C#13 or C#14 features
- We are building an SDK so think of the developer experience.
- For the work on this issue, clone `origin/savasp/refactor`, not `origin/main`
- There is some code in the codebase representing the beginnings of the functionality above but the implementation isn't complete. Examples:
  - `GraphQueryExtensions.cs` with query extensions.
  - Parts of `CypherExpressionBuilder.cs`
  - The `IGraphTraversal.cs` has the main interfaces.

Currently, the query expression tree is translated to cypher in the `CypherExpressionBuilder`. Another approach might be to use the ExpressionVisitor pattern more extensively. If you have suggestions as to the better approach, please feel free to propose them for discussion.

# Re-implementation of the querying functionality

The current Neo4jGraphProvider implementation of the Graph.Model.IGraph interface provides support for serialization/deserialization of in-memory objects to a neo4j graph.

After a refactor, the basic CRUD operations have been re-implemented. Now, I'd like to reimplement the query operations.

The existing implementation attempts to reuse existing LINQ operators. In the new implementation, I'd like to introduce graph-specific operations.

In the previous implementation, there was an assumption that application domain classes representing nodes and relationsihps will have navigation properties. In the new implementation, we will have to find a way to make LINQ graph queries intuitive and type safe without having to explicitly declare navigation properties into the domain specific classes.

As you write code, follow these principles:

- Readable, modular, reusable code
- Use C#13 or C#14 features
- We are building an SDK so think of the developer experience.

# Instructions for the refactoring

Let's do a major refactoring...

Please study the codebase to understand what the current behavior of the Neo4jGraphProvider which is an implementation of the abstractions defined in the Graph.Model project.

We are going to change the serialization requirements. This will simplify the Neo4jGraphProvider's (and most of the classes in that project).

## Main changes

- Classes implementing INode cannot have properties of type R or IEnumerable<R> where R is IRelationship or IRelationship<S, T>
- Classes implementing INode cannot have properteis of type INode or collections of INode, directly or indirectly (ie when a property is a complex type)
- IRelationship<S, T> will no longer have Source and Target properties.

- When creating/updating an INode or an IRelationship in the graph, we will continue to check for a reference cycle in the in-memory object graph.
- INode properties can be complex properties but they are serialized as separate graph nodes, as per the current implementation.
- IRelationshp properties cannot be complex properties.

## Follow these guidelines

- Ground your edits on the existing implementation as much as possible.
- Promote good code readability, modularization, and code reuse
- Use C#13 or C#14 features as much as possible.
- Don't make design decisions on your own. Ask questions before implementing a feature.
- Change the documentation in the code comments, method descriptions, and the markdown files in the repo to match the new behavior.
- Whenever your context fills up, always come back to this document (Refactor.md).

## Here are answers to some questions

- Q: LINQ Provider Changes:
- A: We no longer need that logic. As a result, the implementation should be much simpler.

- Q: Backward Compatibility:
- A: No need to be backwards compatible.

- Q: Relationship Querying: With IRelationship<S, T> losing Source and Target properties, how should developers navigate from relationships to nodes? Should they use separate queries with SourceId and TargetId?
- A: For now, yes... developers will use SourceId and TargetId. After the refactoring, we will indroduce graph querying-specific expressions to help the provider construct the appropriate cypher and project to application domain types.

- Q: TraversalDepth: Since nodes won't have relationship navigation properties, what should TraversalDepth control? Should this option be removed entirely, or repurposed for loading complex node properties?
- A: Yes. The TraversalDepth option should be removed. Complex properties on INode should be represented as graph nodes that are recursively serialized whenever a node is added/updated. They should be recursively deserialized when an graph node is retrieved (via IGraph.GetNode() or a query). The property type rules are recursively applied (e.g. no INode, IRelationship, or collections of them).

- Q: Complex Properties on Nodes: You mentioned these continue to be serialized as separate graph nodes. Should the current TraversalDepth logic be adapted to handle loading these complex properties instead of relationships?
- A: See above. No TraversalDepth is needed. The complex properties will always be serialized/deserialized.

- Q: LINQ Provider Changes: The current Neo4jQueryProvider has extensive logic for applying traversal depth to results. How should this change when navigation properties are removed?
- A: The LINQ provider should only support graph relationship traversal logic for the implicit relationships used to represent complex properties on INode nodes.

- Q: Backward Compatibility: Are there any backward compatibility concerns, or is this a breaking change that's acceptable?
- A: No need for backwards compatibility.

- Q: Migration Strategy: Should we implement this change incrementally (e.g., deprecate navigation properties first) or as a single breaking change?
- A: No need for migration strategy.

- Q: Performance Implications: The current system optimizes for loading connected graphs in single operations. With separate queries required, are you concerned about N+1 query problems?
- A: Not a concern at this stage.

- Q: Relationship Creation: If IRelationship<S, T> no longer has Source and Target properties, how will relationships be created? Should we:
  - Keep constructors that accept source/target nodes but only use them to set SourceId/TargetId?
  - Require explicit setting of SourceId/TargetId?
  - Add factory methods?
- A: We can remove the IRelationship<S, T> interface. It's not needed anymore.

- Q: Navigation Properties Removal: When you say "Classes implementing INode cannot have properties of type R or IEnumerable<R> where R is IRelationship", does this mean:
  - Remove PersonWithNavigationProperty.Knows property entirely?
  - Replace it with some other mechanism for graph traversal?
  - Use the Graph API methods exclusively for relationship queries?
- A: yes, remove PersonWithNavigationProperty.Knows. Use the Graph API methods exclusively for now. Unless you have any recommendations.

- Q: CComplex Property Serialization: You mentioned "INode properties can be complex properties but they are serialized as separate graph nodes" - should this behavior remain the same, or do you want to change how complex properties are handled?
- A: The behavior should remain the same.

- Q: Backward Compatibility: Do we need to maintain backward compatibility with existing tests, or can we update the domain models and tests to match the new design?
- A: update the tests where necessary. Feel free to remove ones that aren't needed anymore. Add new ones to test as much of the functionality as possible.

- Q: API Changes: With navigation properties removed, how should users traverse the graph? Should we enhance the LINQ provider or add new traversal methods?
- A: We will revisit this after the refactoring. For now, let's keep IGraph.Nodes<T>() and IGraph.Relationships<R>() as the way of querying the graph. There are many cypher functions that should be supported even with these simple querying interfaces.

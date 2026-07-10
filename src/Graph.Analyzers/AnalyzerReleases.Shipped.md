## Release 1.0.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
GM001   | Graph.Model | Error    | Missing parameterless constructor or constructor that initializes properties
GM002   | Graph.Model | Error    | Property must have public getters and setters or initializers
GM003   | Graph.Model | Error    | Property cannot be INode or IRelationship type
GM004   | Graph.Model | Error    | Invalid property type for INode implementation
GM005   | Graph.Model | Error    | Invalid property type for IRelationship implementation
GM006   | Graph.Model | Error    | Complex type property contains graph interface types
GM007   | Graph.Model | Error    | Duplicate PropertyAttribute label in type hierarchy
GM008   | Graph.Model | Error    | Duplicate RelationshipAttribute label in type hierarchy
GM009   | Graph.Model | Error    | Duplicate NodeAttribute label in type hierarchy
GM010   | Graph.Model | Error    | Circular reference without nullable type
GM011   | Graph.Model | Warning  | Types should inherit from Node or Relationship base classes instead of implementing INode or IRelationship directly

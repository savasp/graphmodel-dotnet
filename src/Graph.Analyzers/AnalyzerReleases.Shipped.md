## Release 1.0.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
CG001   | Graph.Model | Error    | Missing parameterless constructor or constructor that initializes properties
CG002   | Graph.Model | Error    | Property must have public getters and setters or initializers
CG003   | Graph.Model | Error    | Property cannot be INode or IRelationship type
CG004   | Graph.Model | Error    | Invalid property type for INode implementation
CG005   | Graph.Model | Error    | Invalid property type for IRelationship implementation
CG006   | Graph.Model | Error    | Complex type property contains graph interface types
CG007   | Graph.Model | Error    | Duplicate PropertyAttribute label in type hierarchy
CG008   | Graph.Model | Error    | Duplicate RelationshipAttribute label in type hierarchy
CG009   | Graph.Model | Error    | Duplicate NodeAttribute label in type hierarchy
CG010   | Graph.Model | Error    | Circular reference without nullable type
CG011   | Graph.Model | Warning  | Types should inherit from Node or Relationship base classes instead of implementing INode or IRelationship directly

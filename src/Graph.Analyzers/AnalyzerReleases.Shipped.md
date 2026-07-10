## Release 1.0.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
CG001   | Cvoya.Graph | Error    | Missing parameterless constructor or constructor that initializes properties
CG002   | Cvoya.Graph | Error    | Property must have public getters and setters or initializers
CG003   | Cvoya.Graph | Error    | Property cannot be INode or IRelationship type
CG004   | Cvoya.Graph | Error    | Invalid property type for INode implementation
CG005   | Cvoya.Graph | Error    | Invalid property type for IRelationship implementation
CG006   | Cvoya.Graph | Error    | Complex type property contains graph interface types
CG007   | Cvoya.Graph | Error    | Duplicate PropertyAttribute label in type hierarchy
CG008   | Cvoya.Graph | Error    | Duplicate RelationshipAttribute label in type hierarchy
CG009   | Cvoya.Graph | Error    | Duplicate NodeAttribute label in type hierarchy
CG010   | Cvoya.Graph | Error    | Circular reference without nullable type
CG011   | Cvoya.Graph | Warning  | Types should inherit from Node or Relationship base classes instead of implementing INode or IRelationship directly

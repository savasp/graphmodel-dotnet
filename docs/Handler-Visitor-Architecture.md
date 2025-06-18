# Handler vs Visitor Architecture

This document clarifies the intended architecture for the Cypher query generation system.

## Architecture Overview

The system uses **both** the Visitor pattern and the Handler pattern working together:

1. **Main Query Visitor** (`CypherQueryVisitor`) orchestrates the translation
2. **Method Handlers** handle top-level LINQ query structure operations
3. **Expression Visitors** handle method calls within expressions

## Handler Responsibilities

**Method Handlers** (in `Handlers/` folder) are responsible for:

- **LINQ Query Structure Operations** that build the overall query
- Adding clauses to the `CypherQueryBuilder` (WHERE, RETURN, ORDER BY, etc.)
- Operations that change the query's structure or flow

### Current Handlers:

- `WhereMethodHandler` - Adds WHERE clauses
- `SelectMethodHandler` - Adds RETURN clauses with projections
- `SelectManyMethodHandler` - Handles graph traversals and collection flattening
- `OrderByMethodHandler` / `ThenByMethodHandler` - Add ORDER BY clauses
- `LimitMethodHandler` - Adds LIMIT/SKIP clauses
- `AggregationMethodHandler` - Handles Count, Any, All, First, Single, etc.
- `DistinctMethodHandler` - Adds DISTINCT to queries
- `GroupByMethodHandler` - Handles grouping with collect() functions
- `JoinMethodHandler` - Handles joins using MATCH clauses
- `UnionMethodHandler` - Handles Union/Concat operations
- `GraphOperationMethodHandler` - Handles Include, Traverse, etc.

## Visitor Responsibilities

**Expression Visitors** (in `Expressions/` folder) are responsible for:

- **Method calls within expressions** (e.g., in WHERE conditions)
- Converting method calls to Cypher expressions
- Processing the content of lambda expressions

### Current Expression Visitors:

- `StringMethodVisitor` - Handles string methods like Contains, StartsWith, ToLower
- `CollectionMethodVisitor` - Handles collection methods like Any, All within expressions
- `DateTimeMethodVisitor` - Handles DateTime methods like AddDays, Year property
- `BinaryExpressionVisitor` - Handles binary operations
- `BaseExpressionVisitor` - Handles basic expressions

## Flow Example

```csharp
// LINQ Query:
persons.Where(p => p.Name.Contains("John")).Select(p => p.Name).OrderBy(p => p.Age)

// Processing Flow:
1. CypherQueryVisitor.VisitMethodCall("OrderBy")
   -> OrderByMethodHandler.Handle() -> adds "ORDER BY p.Age"

2. CypherQueryVisitor.VisitMethodCall("Select")
   -> SelectMethodHandler.Handle() -> adds "RETURN p.Name"

3. CypherQueryVisitor.VisitMethodCall("Where")
   -> WhereMethodHandler.Handle()
   -> Uses StringMethodVisitor to process "p.Name.Contains("John")"
   -> StringMethodVisitor converts to "p.Name CONTAINS 'John'"
   -> Adds "WHERE p.Name CONTAINS 'John'"
```

## Key Principles

1. **Handlers are for query structure** - they modify the CypherQueryBuilder
2. **Visitors are for expressions** - they return Cypher expression strings
3. **Handlers coordinate with visitors** - handlers use visitor chains to process lambda expressions
4. **Clear separation of concerns** - top-level operations vs. expression processing

## Integration Points

- Handlers create expression visitor chains using the existing visitors
- The `CypherQueryVisitor` tries handlers first, then falls back to base visitor behavior
- Expression visitors remain unchanged and continue to handle method calls within expressions

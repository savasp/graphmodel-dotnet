# Unified Expression Visitor Architecture

This document describes the current architecture for the Cypher query generation system after the refactoring.

## Architecture Overview

The system now uses a **unified, two-layer approach** based on .NET's `ExpressionVisitor` pattern:

1. **Main Query Orchestrator** (`CypherQueryVisitor`) - Handles LINQ method calls and query structure
2. **Expression Translator** (`ExpressionToCypherVisitor`) - Converts expressions to Cypher strings

## CypherQueryVisitor Responsibilities

**CypherQueryVisitor** (inherits from .NET's `ExpressionVisitor`) is responsible for:

- **LINQ Query Structure Operations** that build the overall query
- Direct handling of LINQ methods: Where, Select, OrderBy, Take, Skip, etc.
- Adding clauses to the `CypherQueryBuilder` (WHERE, RETURN, ORDER BY, etc.)
- Operations that change the query's structure or flow
- Orchestrating the overall query building process

### Handled Operations:

- `Where` - Adds WHERE clauses using expression translation
- `Select` - Adds RETURN clauses with projections
- `OrderBy`/`OrderByDescending` - Add ORDER BY clauses
- `ThenBy`/`ThenByDescending` - Add secondary sorting
- `Take`/`Skip` - Adds LIMIT/SKIP clauses
- `FirstAsync`/`SingleAsync`/etc. - Handles terminating operations with aggregation
- `AnyAsync`/`AllAsync`/`CountAsync` - Handles boolean and count aggregations
- `Distinct` - Adds DISTINCT to queries
- `GroupBy` - Handles grouping with collect() functions
- `Union` - Handles Union/Concat operations

## ExpressionToCypherVisitor Responsibilities

**ExpressionToCypherVisitor** (also inherits from .NET's `ExpressionVisitor`) is responsible for:

- **Expression-to-Cypher Translation** within lambda expressions
- Converting method calls to Cypher expressions (DateTime, String, Math methods)
- Processing the content of lambda expressions used in WHERE, SELECT, etc.
- Handling member access, constants, parameters, and binary operations

### Handled Expression Types:

- **DateTime Methods** - `DateTime.Now`, `AddDays()`, `Year` property, etc.
- **String Methods** - `Contains()`, `StartsWith()`, `ToLower()`, etc.
- **Math Methods** - `Math.Abs()`, `Math.Round()`, etc.
- **Member Access** - Property navigation, complex property handling
- **Binary/Unary Operations** - Comparisons, logical operations
- **Constants and Parameters** - Literal values and lambda parameters

## Example Flow

```csharp
query.Where(p => p.Age > 18 && p.Name.Contains("John"))
     .OrderBy(p => p.Name)
     .Select(p => p.Name)
```

**Processing Flow:**

1. `CypherQueryVisitor.VisitMethodCall()` - Handles "Where"
   - Uses `ExpressionToCypherVisitor` to translate `p.Age > 18 && p.Name.Contains("John")`
   - Adds WHERE clause to query builder
2. `CypherQueryVisitor.VisitMethodCall()` - Handles "OrderBy"
   - Uses `ExpressionToCypherVisitor` to translate `p.Name`
   - Adds ORDER BY clause to query builder
3. `CypherQueryVisitor.VisitMethodCall()` - Handles "Select"
   - Uses `ExpressionToCypherVisitor` to translate `p.Name`
   - Adds RETURN clause to query builder

## Benefits of Unified Architecture

- **Simplified Design**: Two clear responsibilities instead of fragmented handlers/visitors
- **Standard .NET Pattern**: Uses built-in `ExpressionVisitor` base class
- **Better Performance**: Eliminates visitor chaining and handler lookup overhead
- **Easier Maintenance**: Single points of responsibility for each concern
- **Consistent API**: All expression handling follows the same pattern

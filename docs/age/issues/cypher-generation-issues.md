# Cypher Generation Issues

> Cypher generation findings from the AGE provider review. All 15 issues are documented below, including 7 actionable items and 8 verified-correct (informational) findings.

---

## H1: String Methods Use `=~` POSIX Regex Instead of Native Operators

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`StringMethodHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) |
| **Status** | ✅ Resolved — replaced =~ regex with native CONTAINS/STARTS WITH/ENDS WITH operators |

### Description

[`StringMethodHandler`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) translates .NET string methods (`Contains`, `StartsWith`, `EndsWith`) to POSIX regex patterns using the `=~` operator instead of native Cypher operators:

| .NET Method | Current Cypher Output | Expected Cypher Output |
|-------------|----------------------|------------------------|
| `s.Contains("x")` | `s =~ '.*x.*'` | `s CONTAINS 'x'` |
| `s.StartsWith("x")` | `s =~ '^x.*'` | `s STARTS WITH 'x'` |
| `s.EndsWith("x")` | `s =~ '.*x$'` | `s ENDS WITH 'x'` |

Using `=~` has several drawbacks:
- **Performance**: Regex matching is significantly slower than the native string operators, which use optimized string search algorithms.
- **Readability**: The generated Cypher is harder to read and audit.
- **Special character handling**: Regex metacharacters in the search string (`.` `*` `?` `+` `[` `]` `(` `)` `{` `}` `^` `$` `\`) need escaping; native operators handle them as literals.
- **Index usage**: PostgreSQL/AGE may not use indexes effectively with regex patterns compared to native operators.

### Impact

- **Performance degradation**: Regex matching is slower than native string operators, especially for large datasets.
- **Incorrect results**: Regex metacharacters in search strings produce incorrect matches (e.g., searching for `"foo.bar"` matches `"fooXbar"` because `.` matches any character).
- **Index inefficiency**: The `=~` operator may prevent use of GiST or GIN indexes that support `CONTAINS`/`STARTS WITH`/`ENDS WITH`.

### Exploitation Scenario

A query filtering `users.Where(u => u.Name.Contains("admin."))` would produce `n.Name =~ '.*admin..*'` — the `.` matches any character, so it would also match `"adminX"` which was never intended.

### Recommended Fix

Replace the `=~` regex patterns with native Cypher `CONTAINS`, `STARTS WITH`, and `ENDS WITH` operators.

### Implementation Options

- **Option A**: Replace regex patterns with native operators for constant arguments
  - For constant string arguments, emit `CONTAINS`, `STARTS WITH`, or `ENDS WITH` directly
  - This handles the most common case (compile-time constant search strings) efficiently

- **Option B**: Also update fallback path for dynamic arguments
  - For dynamic arguments (expressions resolved at runtime), still use native operators with parameterized values
  - This ensures consistency regardless of argument type

### Acceptance Criteria

- [ ] `Contains` → `CONTAINS` (not `=~`)
- [ ] `StartsWith` → `STARTS WITH` (not `=~`)
- [ ] `EndsWith` → `ENDS WITH` (not `=~`)
- [ ] Regex metacharacters in search strings are treated as literals
- [ ] All existing string method tests pass with updated Cypher output
- [ ] Performance benchmark shows improvement for string filtering queries

### References

- [Cypher `CONTAINS` / `STARTS WITH` / `ENDS WITH` Operators](https://neo4j.com/docs/cypher-manual/current/syntax/operators/#query-syntax-string-predicates)
- See also: [F-07](security-issues.md#f-07-containsstartswithendswith-fallback-uses-string-concatenation) (security implications of the concatenation approach)

---

## H2: `toInteger()` Not Emitted for Explicit Numeric Conversions

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`AgeExpressionToCypherVisitor.cs:266`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:266) |
| **Status** | ✅ Resolved — all integer-like types now emit toInteger() wrapper |

### Description

When an explicit numeric cast like `(int)expression` or `(long)expression` is used in a LINQ query, the generated Cypher does not wrap the expression with [`toInteger()`](https://age.apache.org/age-manual/master/functions.html). However, `(double)expression` correctly emits [`toFloat()`](https://age.apache.org/age-manual/master/functions.html).

```csharp
// LINQ: users.Where(u => (int)u.Score > 10)
// Current Cypher: n.score > 10  (no toInteger() wrapper — potential type mismatch)
// Expected Cypher: toInteger(n.score) > 10

// LINQ: users.Where(u => (double)u.Score > 10.5)
// Current Cypher: toFloat(n.score) > 10.5  (correct)
```

This inconsistency means integer casts may produce Cypher that fails with type errors if the underlying property type doesn't match the expected integer type in AGE.

### Impact

- **Type mismatch errors**: AGE may reject the query or produce incorrect results when the property type doesn't match the expected integer type.
- **Inconsistent behavior**: `(double)` casts work correctly but `(int)`/`(long)`/`(short)`/`(byte)` casts do not.
- **Silent truncation**: Without explicit `toInteger()`, AGE might perform implicit conversion that differs from .NET's expected behavior.

### Recommended Fix

Add explicit type checks for all integer-like types (`int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `ulong`) and emit [`toInteger()`](https://age.apache.org/age-manual/master/functions.html) for their conversions.

### Implementation Options

- **Option A**: Add type checks for all integer types and emit `toInteger()`
  - Extend the [`ConvertToCypherType`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs) logic to check for `typeof(int)`, `typeof(long)`, `typeof(short)`, `typeof(byte)`, `typeof(sbyte)`, `typeof(ushort)`, `typeof(uint)`, `typeof(ulong)`
  - Emit `toInteger(expr)` for any of these types

- **Option B**: Create a `ConvertToCypherType()` helper method
  - Extract the type-to-Cypher-function mapping into a dedicated method
  - Unit test the mapping independently of the visitor
  - This centralizes the conversion logic for easier maintenance

### Acceptance Criteria

- [ ] `(int)expr` → `toInteger(expr)`
- [ ] `(long)expr` → `toInteger(expr)`
- [ ] `(short)expr` → `toInteger(expr)`
- [ ] `(byte)expr` → `toInteger(expr)`
- [ ] `(double)expr` → `toFloat(expr)` (already works — verify no regression)
- [ ] Tests verify each numeric conversion type

### References

- [Apache AGE Functions — toInteger()](https://age.apache.org/age-manual/master/functions.html)

---

## H3: Column Definitions Double-Quote Identifiers — Fragile Casing

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`ColumnDefinitionBuilder.cs:42`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:42) |
| **Status** | ✅ Resolved — column names validated against identifier regex and documented |

### Description

[`ColumnDefinitionBuilder`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs) always double-quotes column identifiers in the generated SQL column definitions:

```csharp
// Always double-quotes: "column_name" type
$"""{columnName}""" {type}
```

This is fragile because the column names in the [`RETURN`](https://opencypher.org/public/2023-05-21/docs/#return) clause of the Cypher query (inside the `$$ ... $$` block) must match these quoted identifiers exactly, including casing. If the Cypher query uses a different casing for alias names (e.g., `RETURN n.Name AS name` vs `RETURN n.Name AS "Name"`), the query will fail with a column-not-found error.

### Impact

- **Fragile queries**: Column definition casing must match Cypher `RETURN` alias casing exactly. Any mismatch causes a runtime database error.
- **Debugging difficulty**: Column-not-found errors are hard to trace back to casing mismatches.
- **Refactoring hazard**: Changing alias casing in one place breaks the other without compile-time detection.

### Recommended Fix

Add a test to verify that `RETURN` aliases match SQL column definitions, and ideally extract alias generation to a single source of truth.

### Implementation Options

- **Option A**: Add automated verification test
  - Create a test that extracts the `RETURN` clause alias names and verifies they match the generated column definitions
  - Run this test for every query type
  - This ensures the two stay in sync

- **Option B**: Extract alias generation to single source of truth
  - Create a dedicated `AliasGenerator` type that defines aliases once
  - Both the column definition builder and the Cypher visitor reference the same generator
  - This eliminates the possibility of drift

### Acceptance Criteria

- [ ] A test verifies that `RETURN` aliases match column definitions
- [ ] (Optional) Alias generation is centralized in a single type
- [ ] No regression in existing column definition behavior

### References

- [AGE Column Definitions](https://age.apache.org/age-manual/master/using_age.html#column-definitions)

---

## M1: Parameter Deduplication Uses Value Equality

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`QueryParameterStore.cs:28`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:28) |
| **Status** | ✅ Resolved — replaced with Dictionary-based O(1) dedup using EqualityComparer.Default |

### Description

[`QueryParameterStore`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs) uses [`Object.Equals()`](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals) for deduplicating query parameters:

```csharp
// Line 28: Equals on unconstrained object? for dedup
if (!_parameters.Any(p => Equals(p.Value, value)))
```

This is problematic because:
- `Equals()` can throw for custom types that don't implement it properly
- Value equality semantics vary by type (reference types compare by reference by default)
- Floating-point `NaN` equality behaves unexpectedly (`NaN != NaN`)
- Complex types (arrays, collections) may not have the desired equality semantics

### Impact

- **Incorrect deduplication**: Some values may be incorrectly considered duplicates (or not duplicates) due to unexpected `Equals` behavior.
- **Runtime exceptions**: Custom types with broken `Equals` implementations can throw.
- **Unpredictable parameter naming**: Deduplication directly affects parameter names in the generated Cypher, changing query text unexpectedly.

### Recommended Fix

Use [`EqualityComparer<object?>.Default`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default) for consistent equality behavior, or switch to a dictionary-based approach.

### Implementation Options

- **Option A**: Switch to [`EqualityComparer<object?>.Default`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default)
  - Use `EqualityComparer<object?>.Default.Equals(a, b)` instead of `Equals(a, b)`
  - This provides consistent handling of null, `NaN`, and default equality

- **Option B**: Only dedup primitive types; skip dedup for complex types
  - Check if the value is a primitive, string, or common simple type
  - Only attempt deduplication for known-safe types
  - For complex types, always add a new parameter entry

### Acceptance Criteria

- [ ] Parameter deduplication uses a consistent equality comparer
- [ ] No `Equals()` is called on unconstrained `object?` values
- [ ] Known edge cases (null, NaN, custom types) are handled safely
- [ ] All existing tests continue to pass

### References

- [EqualityComparer<T>.Default](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.equalitycomparer-1.default)

---

## M2: graphName Directly Interpolated into SQL

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`ColumnDefinitionBuilder.cs:102`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) |
| **Status** | ✅ Resolved — graphName parameterized via NpgsqlParameter (see F-01) |

### Description

The `graphName` parameter is directly interpolated into the SQL string in [`ColumnDefinitionBuilder.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs):

```csharp
$"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {cypher} $$, $1) as {columnDefinitions};"
```

This is documented as a **Critical** security issue in [F-01](security-issues.md#f-01-sql-injection-via-graphname-in-ag_catalogcypher-wrapper). From the Cypher generation perspective, this is also a correctness issue — a malformed `graphName` can corrupt the entire query, not just inject SQL.

### Impact

- **SQL injection** (Critical severity)
- **Query corruption**: Even without malicious intent, a `graphName` containing special characters (e.g., hyphens, spaces) can break the SQL syntax.

### Recommended Fix

(Detailed remediation provided in [F-01](security-issues.md#f-01-sql-injection-via-graphname-in-ag_catalogcypher-wrapper).) Parameterize `graphName` via [`NpgsqlParameter`](https://www.npgsql.org/doc/parameters.html).

### References

- [F-01: SQL Injection via graphName](security-issues.md#f-01-sql-injection-via-graphname-in-ag_catalogcypher-wrapper)

---

## M3: FallbackEvaluate Silently Catches and Throws Generic Error

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`MemberExpressionHandler.cs:449`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs:449) |
| **Status** | ✅ Resolved — original exception preserved as inner exception; logged at Warning level |

### Description

In [`MemberExpressionHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs), the `FallbackEvaluate` method catches exceptions during expression evaluation and throws a generic error:

```csharp
try
{
    return Expression.Lambda<Func<object?>>(member).Compile()();
}
catch (Exception ex)
{
    // Generic error — loses the original exception details
    throw new NotSupportedException($"Cannot evaluate member expression: {member}");
}
```

The generic [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception) discards:
- Whether the failure was a compilation error (expression cannot be compiled)
- Whether the failure was a runtime error (compilation succeeded but execution threw)
- The original exception type (e.g., [`NullReferenceException`](https://learn.microsoft.com/en-us/dotnet/api/system.nullreferenceexception) vs [`InvalidCastException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidcastexception))

### Impact

- **Lost diagnostic information**: Developers can't distinguish between different failure modes.
- **Confusing error messages**: `"Cannot evaluate member expression"` without context of why.
- **Harder debugging**: Stack traces point to the rethrow location, not the original failure.

### Recommended Fix

Log the original exception details before throwing the generic error. Consider including the inner exception.

### Implementation Options

- **Option A**: Log the member expression that failed and why
  - Log at `Warning` level with the member expression details and original exception
  - Include the original exception as an inner exception: `new NotSupportedException(msg, ex)`

- **Option B**: Include inner exception in thrown [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception)
  - Preserve the original exception as the inner exception
  - This allows callers to inspect the root cause if needed

### Acceptance Criteria

- [ ] Original exception details are preserved or logged
- [ ] The error message includes the member expression that caused the failure
- [ ] Tests verify that failures produce informative error output

### References

- [Exception.InnerException Property](https://learn.microsoft.com/en-us/dotnet/api/system.exception.innerexception)

---

## M4: `size()` for string `.Length` Could Conflict with `size()` List Semantics

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Cypher Generation |
| **Source Review** | Cypher Generation Review |
| **Affected File(s)** | [`StringMethodHandler.cs:74`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:74) |
| **Status** | ✅ Resolved — added code comment explaining size() overloaded nature |

### Description

The [`size()`](https://age.apache.org/age-manual/master/functions.html) function in Cypher is overloaded — it works for both strings (returns character count) and lists (returns element count). When [`StringMethodHandler`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) translates `.Length` to `size(property)`, the semantics differ subtly:

```csharp
// LINQ: s.Length
// Cypher: size(s) — works for both strings and lists
```

While functionally correct (both return the count), the overloaded `size()` function can cause confusion:
- If a property type is changed from `string` to `List<T>`, the same Cypher `size()` produces a different semantic meaning
- In error messages or query logs, `size()` on a string looks identical to `size()` on a list

### Impact

- **Low-severity confusion**: The overloaded function works correctly but can be confusing in query analysis and debugging.
- **Potential future conflict**: If AGE ever differentiates `size_string()` and `size_list()` functions, the generated Cypher would need updating.

### Recommended Fix

No code change required. Add a documentation comment explaining the choice and its implications.

### Implementation Options

- **Option A**: Add explanatory comment in code
  - Add a comment noting that `size()` is used for `.Length` because it works for both strings and lists
  - Note the overloaded nature and potential future changes

- **Option B**: Track property type metadata to use correct function
  - If the visitor tracks whether a property is a string or list, use `length()` for strings and `size()` for lists
  - This is more precise but adds complexity

### Acceptance Criteria

- [ ] A code comment documents the `size()` vs `length()` decision
- [ ] (Optional) Property type metadata is used to select the precise function
- [ ] Existing tests continue to pass

### References

- [Apache AGE Functions — size()](https://age.apache.org/age-manual/master/functions.html)

---

## L1–L8: Verified Correct — No Action Needed

The following findings were reviewed and confirmed to be correct. No changes are required.

---

### L1: Boolean Deserialization Correct ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

The library correctly deserializes boolean values from AGE's `agtype` representation. AGE returns booleans as `true`/`false` (lowercase) in its JSON-like output format, and the serializer correctly maps these to .NET `bool` values.

No changes required.

---

### L2: CASE WHEN Expressions Correct ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

[`CASE WHEN`](https://opencypher.org/public/2023-05-21/docs/#case-expression) expressions (generated for conditional queries or certain LINQ patterns) are correctly formatted in Cypher syntax. The generated expressions follow the correct `CASE WHEN condition THEN result ELSE default END` structure and are compatible with Apache AGE's parser.

No changes required.

---

### L3: Null Comparison Handling Correct ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

Null comparisons in LINQ (e.g., `Where(u => u.Name == null)`) are correctly translated to `IS NULL` in Cypher. Non-null comparisons (`Where(u => u.Name != null)`) correctly translate to `IS NOT NULL`. The null handling follows Cypher's three-valued logic (`TRUE`, `FALSE`, `NULL`).

No changes required.

---

### L4: Predicate Push-Down Optimization Safe ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

The library implements predicate push-down optimization, where filter conditions are applied as early as possible in the query to reduce the working set. This optimization:

- Pushes filter conditions into `MATCH` patterns where possible
- Uses `WHERE` clauses at the correct scope level
- Does not incorrectly push predicates past aggregation boundaries

The implementation is correct and safe for all observed patterns.

No changes required.

---

### L5: Degree Query Handling Avoids `size()` Pattern ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

Degree queries (counting relationships of a node) correctly use Cypher's pattern matching rather than the `size()` function on relationship collections. For example:

```cypher
// Correct: MATCH (n)-[r]->() RETURN count(r)
// Not: RETURN size((n)-[]->())
```

This is correct because:
- `size()` on a pattern returns the number of matching paths, which is equivalent
- Using explicit `MATCH` + `count()` is more readable and compatible with all AGE versions

No changes required.

---

### L6: DateTime Handling Correct ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

[`DateTime`](https://learn.microsoft.com/en-us/dotnet/api/system.datetime) and [`DateTimeOffset`](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset) values are correctly serialized to AGE's timestamp format. The conversions handle:

- `DateTime` → AGE `timestamp` (with timezone awareness based on `DateTime.Kind`)
- `DateTimeOffset` → AGE `timestamptz`
- Round-trip fidelity through AGE's timestamp representation

No changes required.

---

### L7: `RETURN *` Never Used ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

The library never generates [`RETURN *`](https://opencypher.org/public/2023-05-21/docs/#return) in its Cypher output. Instead, it always generates explicit column lists:

```cypher
-- Correct: RETURN n.id, n.name, n.age
-- Not: RETURN *
```

This is correct because:
- Explicit column lists are more maintainable and self-documenting
- `RETURN *` can expose internal properties or change behavior when the schema changes
- Apache AGE has specific requirements for column definitions that align with explicit returns

No changes required.

---

### L8: Aggregation Functions Use Correct AGE Names ✅

| Field | Value |
|-------|-------|
| **Severity** | ✅ Verified Correct |
| **Category** | Cypher Generation |
| **Status** | Closed — No Action Needed |

#### Description

The library correctly maps .NET aggregation methods to AGE aggregation functions:

| .NET Method | AGE Function |
|-------------|-------------|
| `.Sum()` | `sum()` |
| `.Average()` | `avg()` |
| `.Min()` | `min()` |
| `.Max()` | `max()` |
| `.Count()` | `count()` |

These function names are consistent with both the [openCypher standard](https://opencypher.org/public/2023-05-21/docs/#aggregate-functions) and Apache AGE's implementation.

No changes required.

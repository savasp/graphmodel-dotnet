# Security Issues

> Security findings from the AGE provider review. All 14 issues are documented below with severity ratings, impact analysis, exploitation scenarios, and recommended fixes.

---

## F-01: SQL Injection via `graphName` in `ag_catalog.cypher()` Wrapper

| Field | Value |
|-------|-------|
| **Severity** | 🔴 Critical |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`ColumnDefinitionBuilder.cs:102`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) |
| **Status** | ✅ Resolved — graphName is now passed as parameter via NpgsqlParameter |

### Description

The [`graphName`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) string is directly interpolated into an SQL string passed to Npgsql:

```csharp
$"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {cypher} $$, $1) as {columnDefinitions};"
```

Because [`graphName`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) is a user-supplied value (or derived from potentially untrusted input in some contexts), this creates a first-order SQL injection vector. An attacker who controls `graphName` can break out of the string literal context and execute arbitrary SQL against the PostgreSQL backend.

### Impact

- **Full database compromise**: Arbitrary SQL execution allows an attacker to read, modify, or delete any data in the database, including data in other graphs or schemas.
- **Privilege escalation**: If the connection uses a high-privilege account (e.g., `postgres` superuser), the attacker can execute administrative operations.
- **Data exfiltration**: All graph data and relational data accessible to the connection can be extracted.

### Exploitation Scenario

An attacker who can influence the value of `graphName` (e.g., via a configuration injection, a compromised upstream service, or a malicious input that flows into graph selection) could supply:

```
graphName = "'; DROP TABLE ag_catalog.ag_graph CASCADE; SELECT '"
```

This would result in the following SQL being executed:

```sql
SELECT * FROM ag_catalog.cypher(''; DROP TABLE ag_catalog.ag_graph CASCADE; SELECT '', $$ ... $$, $1) as (...);
```

PostgreSQL would terminate the first string, execute the DROP statement, and potentially destroy graph metadata.

### Recommended Fix

Replace string interpolation with parameterized SQL using [`NpgsqlParameter`](https://www.npgsql.org/doc/parameters.html). The `graphName` value should be passed as a parameter rather than embedded in the SQL string.

### Implementation Options

- **Option A**: Use [`NpgsqlParameter`](https://www.npgsql.org/doc/parameters.html) for `graphName`
  - Modify the SQL to use a parameter placeholder: `"SELECT * FROM ag_catalog.cypher(@graphName, $$ ... $$, $1) as ..."`
  - Add `cmd.Parameters.AddWithValue("graphName", graphName)`
  - This is the strongest defense and follows Npgsql best practices.

- **Option B**: Validate `graphName` against a regex of allowed identifiers
  - Validate `graphName` matches `^[a-zA-Z_][a-zA-Z0-9_]*$` before interpolation
  - Throw [`ArgumentException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception) on validation failure
  - This is a defense-in-depth measure but does not replace parameterization.

### Acceptance Criteria

- [ ] `graphName` is passed as a parameter (not interpolated) in the [`ColumnDefinitionBuilder.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs) SQL construction
- [ ] Existing tests continue to pass
- [ ] A negative test confirms that a malicious `graphName` value is rejected or safely parameterized

### References

- [OWASP SQL Injection Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html)
- [Npgsql Documentation — Parameters](https://www.npgsql.org/doc/parameters.html)
- See also: [M2](cypher-generation-issues.md#m2-graphname-directly-interpolated-into-sql) (cross-reference in Cypher Generation issues)

---

## F-02: SQL Injection via `columnDefinitions` in `ag_catalog.cypher()` Wrapper

| Field | Value |
|-------|-------|
| **Severity** | 🔴 Critical |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`ColumnDefinitionBuilder.cs:34`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:34) |
| **Status** | ✅ Resolved — column names validated against identifier regex and quote-identified |

### Description

The `columnDefinitions` string — built from LINQ member names in [`ColumnDefinitionBuilder.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs) — is interpolated directly into the SQL query without sanitization:

```csharp
$"SELECT * FROM ag_catalog.cypher('{graphName}', $$ {cypher} $$, $1) as {columnDefinitions};"
```

Column names derived from .NET member names (properties, fields) could contain characters or be constructed in ways that break the SQL syntax. Although member names in well-behaved code are typically alphanumeric, computed columns or dynamic entity types could introduce unsafe values.

### Impact

- **SQL injection**: Arbitrary SQL can be injected through the column definition clause.
- **Query corruption**: Even without injection, malformed column definitions can cause query failures or incorrect results.
- **Information disclosure**: Carefully crafted column definitions could be used in error-based information extraction.

### Exploitation Scenario

If a dynamic entity type has a property with a crafted name like:

```
"ColumnName); DROP TABLE ag_catalog.ag_graph CASCADE; SELECT (1"
```

The resulting SQL would include this name in the `AS` clause, breaking the syntax and potentially executing injected SQL.

### Recommended Fix

Quote-identify all column names and reject names containing SQL metacharacters before they reach the SQL construction point.

### Implementation Options

- **Option A**: Sanitize each column name with a whitelist regex and quote-identify
  - Validate each column name matches `^[a-zA-Z_][a-zA-Z0-9_]*$`
  - Quote-identify using Npgsql's `QuotedIdentifier` or double-quote escaping
  - Throw on validation failure

- **Option B**: Restrict column names to alphanumeric + underscore pattern
  - Strip or reject any column name containing non-alphanumeric characters (except underscore)
  - This is a more restrictive approach that may reject legitimate edge cases

### Acceptance Criteria

- [ ] Column names are validated against a strict identifier pattern before SQL construction
- [ ] Column names are quote-identified in the SQL output
- [ ] Existing tests pass; new negative tests verify injection resistance

### References

- [OWASP SQL Injection Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html)

---

## F-03: Default Hardcoded Database Credentials

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeGraphStore.cs:81`](../../../src/Graph.Model.Age/Core/AgeGraphStore.cs:81) |
| **Status** | ✅ Resolved — hardcoded credentials replaced with throw of InvalidOperationException |

### Description

The [`AgeGraphStore`](../../../src/Graph.Model.Age/Core/AgeGraphStore.cs) constructor falls back to a hardcoded connection string when neither a supplied connection string nor the `AGE_CONNECTION_STRING` environment variable is set:

```csharp
connectionString ??= Environment.GetEnvironmentVariable("AGE_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
```

The credential `postgres:postgres` is the well-known default password for the PostgreSQL superuser account. If this fallback is ever reached in production (e.g., due to a deployment configuration error), the database is effectively open to anyone who can reach the host.

### Impact

- **Unauthorized database access**: Anyone who discovers or probes the PostgreSQL host can authenticate with `postgres:postgres` and gain superuser access.
- **Data breach**: All graph data and any other data in the database is exposed.
- **Compliance violation**: Hardcoded credentials in source code violate PCI-DSS, SOC2, and many other compliance frameworks.

### Exploitation Scenario

A deployment fails to set `AGE_CONNECTION_STRING` in the environment, and the application starts with the default credential. An internal network scanner probing port 5432 can authenticate to the database using the well-known default credentials.

### Recommended Fix

Remove the fallback credential default. If no connection string is configured, the application should fail with a clear error message rather than silently using insecure defaults.

### Implementation Options

- **Option A** (Recommended): Remove the default entirely and throw [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception)
  - Replace the fallback with: `throw new InvalidOperationException("No connection string configured. Set AGE_CONNECTION_STRING environment variable or pass a connectionString parameter.")`
  - This ensures that misconfigured deployments fail fast and visibly.

- **Option B**: Keep the default but add [`[Obsolete]`](https://learn.microsoft.com/en-us/dotnet/api/system.obsoleteattribute) warning and a large code doc warning
  - Add `[Obsolete("The hardcoded fallback connection string is insecure and will be removed in a future version. Always configure AGE_CONNECTION_STRING.")]`
  - Update XML documentation to clearly warn about this being for development only

### Acceptance Criteria

- [ ] The hardcoded `"postgres"` password credential is removed from [`AgeGraphStore.cs`](../../../src/Graph.Model.Age/Core/AgeGraphStore.cs)
- [ ] An appropriate exception is thrown when no connection string is configured
- [ ] The error message clearly instructs the developer on how to configure the connection string properly
- [ ] Tests are updated to supply an explicit connection string

### References

- [CWE-798: Use of Hard-coded Credentials](https://cwe.mitre.org/data/definitions/798.html)
- [OWASP: Hardcoded passwords](https://owasp.org/www-community/vulnerabilities/Use_of_hard-coded_password)

---

## F-04: Closure-Captured Expression Compilation at Query Time

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`ClosureCaptureHandler.cs:70-71`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/ClosureCaptureHandler.cs:70-71), [`AgeExpressionToCypherVisitor.cs:237-239`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:237-239) |
| **Status** | ✅ Resolved — added method allowlist and expression compilation caching |

### Description

The expression tree visitor compiles and executes arbitrary expression tree nodes at query-translation time via [`Expression.Lambda<Func<object>>(...).Compile()`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile). Specifically:

- [`ClosureCaptureHandler.cs:70-71`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/ClosureCaptureHandler.cs:70-71) evaluates closure-captured values
- [`AgeExpressionToCypherVisitor.cs:237-239`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:237-239) compiles unrecognized method calls as a fallback

This turns the query translation pipeline into a general-purpose code execution engine. A malicious LINQ query could invoke arbitrary code during translation, before the query ever reaches the database.

### Impact

- **Remote code execution (RCE)**: An expression tree containing a malicious method call (e.g., `Process.Start("cmd.exe", "/c ...")`, file I/O, network calls) would be compiled and executed.
- **Denial of service**: An expression that causes infinite recursion or memory allocation could crash the application.
- **Data exfiltration**: An expression could read local files, environment variables, or network resources and embed them in the query output.

### Exploitation Scenario

If user input flows into a LINQ expression that is then translated by `Graph.Model.Age` (e.g., via a dynamic query builder or an API that accepts expression trees), an attacker could craft:

```csharp
x => MethodThatDoesSomethingMalicious()
```

Where `MethodThatDoesSomethingMalicious()` is any accessible static method. The expression visitor would compile this expression and execute it, performing the malicious action.

### Recommended Fix

Avoid [`Compile()`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile) where possible. When compilation is unavoidable, add a timeout, limit which member types can be compiled, and maintain an allowlist.

### Implementation Options

- **Option A**: Add a [`CancellationToken`](https://learn.microsoft.com/en-us/dotnet/api/system.threadting.cancellationtoken) and limit compilation to static members only
  - Pass a `CancellationToken` through the visitor hierarchy
  - Add a timeout mechanism around `Compile()` calls
  - Reject non-static method compilation
  - Log all compiled expressions at Warning level

- **Option B**: Cache compiled delegates in a [`ConcurrentDictionary<Expression, object?>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)
  - Cache results per expression instance to prevent repeated compilation
  - Add an upper bound on cache size
  - This mitigates performance DoS but not arbitrary code execution

### Acceptance Criteria

- [ ] Expression compilation is restricted to an allowlist of safe operations
- [ ] A `CancellationToken` or timeout is enforced during compilation
- [ ] Logging is added for any expression that requires compilation at translation time
- [ ] Tests confirm that malicious expressions are rejected

### References

- [CWE-94: Improper Control of Generation of Code (Code Injection)](https://cwe.mitre.org/data/definitions/94.html)
- [LINQ Expression Trees Security Considerations (Microsoft)](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/#security-considerations)

---

## F-05: Expression Tree Compilation for Unresolvable Method Calls

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeExpressionToCypherVisitor.cs:237-239`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:237-239) |
| **Status** | ✅ Resolved — allowlist of 60+ known-safe methods; unrecognized methods throw NotSupportedException |

### Description

In [`AgeExpressionToCypherVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs), all unrecognized [`MethodCallExpression`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.methodcallexpression) nodes fall through to a general-purpose compilation and execution path. This means any .NET method — including system-level and user-defined methods — can be invoked during query translation if the visitor does not have an explicit handler for it.

### Impact

- **Arbitrary code execution**: Any .NET method that is accessible in the expression context can be invoked.
- **Bypass of security boundaries**: Methods like `File.WriteAllText`, `Process.Start`, `Registry.SetValue`, etc. could be called through a crafted expression.
- **Unpredictable behavior**: The set of invocable methods is unbounded, making the security surface impossible to assess comprehensively.

### Exploitation Scenario

An attacker who controls the content of a LINQ expression (e.g., via an API that accepts `Expression<Func<...>>` from untrusted input) could include a method call like:

```csharp
users.Where(u => File.WriteAllText(@"C:\malicious\output.txt", u.SensitiveData))
```

The expression visitor, unable to recognize `File.WriteAllText` as a known handler, would compile and execute it.

### Recommended Fix

Add an explicit allowlist of known-safe methods. Throw [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception) for any method not on the allowlist.

### Implementation Options

- **Option A**: Maintain a [`HashSet<string>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1) of known safe methods
  - Build a `HashSet<string>` of fully-qualified method names that are safe to compile
  - Include only methods that are pure, side-effect-free operations (e.g., `Math.Max`, `String.Concat`)
  - Throw `NotSupportedException` for any unrecognized method

- **Option B**: Only allow evaluation of parameterless static methods
  - Restrict compilation to methods matching `typeof(SomeType).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.GetParameters().Length == 0)`
  - This dramatically reduces the attack surface but may break legitimate usage

### Acceptance Criteria

- [ ] An explicit allowlist is defined for methods that can be compiled at query time
- [ ] Unrecognized methods throw `NotSupportedException` with a clear message
- [ ] Existing functionality that depends on fallback compilation is migrated to explicit handlers
- [ ] Tests verify both allowed and rejected method scenarios

### References

- [CWE-94: Improper Control of Generation of Code (Code Injection)](https://cwe.mitre.org/data/definitions/94.html)

---

## F-06: DynamicNode Property Names Inlined into Cypher SET Statements

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeNodeManager.cs:76`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:76), [`AgeRelationshipManager.cs:67`](../../../src/Graph.Model.Age/Core/Entities/AgeRelationshipManager.cs:67) |
| **Status** | ✅ Resolved — property names backtick-quoted via QuotePropertyName helper |

### Description

Property names for dynamic entities (those implementing [`IDynamicEntity`](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.idynamicentity) or similar) are mapped via [`MapPropertyNameForAge()`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:76) and then interpolated directly into Cypher `SET` statements without sanitization or quoting.

```csharp
// Pseudocode of the pattern
$"SET n.{propertyName} = {value}"
```

If a property name contains special characters or is derived from untrusted input, this creates a Cypher injection vector.

### Impact

- **Cypher injection**: Property names with special characters can break out of the identifier context and inject arbitrary Cypher.
- **Data corruption**: Injected Cypher could modify or delete unintended data.
- **Limited scope compared to SQL injection**: The injection is contained within the Cypher query context, but can still alter query semantics significantly.

### Exploitation Scenario

A dynamic entity with a property name like:

```
"name = 'abc' REMOVE n"
```

Would result in a Cypher query that sets `name` to `'abc'` and then removes property `n` — an unintended side effect.

### Recommended Fix

Escape property names using backtick quoting (Cypher's identifier quoting mechanism) before interpolation. All dynamic property names should be quoted.

### Implementation Options

- **Option A**: Use [`CypherQueryHelper.EscapePropertyName()`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryHelper.cs) (or equivalent) and backtick-quote all identifiers
  - Create a utility method `EscapePropertyName(string name)` that wraps the name in backticks and escapes any backticks within
  - Apply this in all locations where property names are interpolated into Cypher

- **Option B**: Add a centralized `SanitizePropertyName()` method
  - Strip or reject property names that contain characters outside `[a-zA-Z0-9_]`
  - Apply this validation in [`MapPropertyNameForAge()`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:76)
  - More restrictive but provides defense-in-depth

### Acceptance Criteria

- [ ] All dynamic property names are backtick-quoted when interpolated into Cypher
- [ ] A utility method for property name escaping exists and is used consistently
- [ ] Tests confirm that property names with special characters are handled safely

### References

- [Cypher Identifier Quoting — openCypher Spec](https://opencypher.org/resources/)

---

## F-07: Contains/StartsWith/EndsWith Fallback Uses String Concatenation

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`StringMethodHandler.cs:108-109`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:108-109) |
| **Status** | ✅ Resolved — replaced =~ regex with native CONTAINS/STARTS WITH/ENDS WITH operators |

### Description

When [`StringMethodHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) cannot resolve a string method argument at compile time (i.e., it's a dynamic expression), it falls back to a POSIX regex pattern built via string concatenation:

```csharp
$"{obj} =~ ('.*' + {substringCypher} + '.*')"
```

This string concatenation approach is vulnerable to Cypher injection — if the `substringCypher` value contains Cypher metacharacters, they are inserted directly into the expression.

### Impact

- **Cypher injection**: A crafted dynamic value can inject arbitrary Cypher through the regex pattern.
- **Query corruption**: Even without injection, the concatenation can produce syntactically invalid Cypher.
- **Regex injection**: The `/` and other regex metacharacters in the substring value can alter the regex behavior.

### Exploitation Scenario

A dynamic argument value containing `' + n.AnotherProp + '` would result in a Cypher expression that accesses a different property than intended, potentially leaking data.

### Recommended Fix

Use native Cypher `CONTAINS`, `STARTS WITH`, or `ENDS WITH` operators instead of `=~` regex matching. These operators accept parameterized values and avoid injection.

### Implementation Options

- **Option A** (Recommended): Switch to native operators entirely
  - Replace `=~` with `CONTAINS`, `STARTS WITH`, or `ENDS WITH`
  - These operators treat their arguments as values (not expressions to concatenate), eliminating injection
  - Example: `n.Name CONTAINS $searchTerm`

- **Option B**: Only support compile-time-constant arguments; throw for dynamic arguments
  - If the substring argument is not a constant, throw [`NotSupportedException`](https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception)
  - This eliminates the injection vector but also removes functionality

### Acceptance Criteria

- [ ] Native `CONTAINS`/`STARTS WITH`/`ENDS WITH` operators are used instead of `=~` regex
- [ ] All string method tests (constant and dynamic arguments) pass
- [ ] A negative test confirms injection resistance

### References

- [Cypher `CONTAINS` / `STARTS WITH` / `ENDS WITH` Operators](https://neo4j.com/docs/cypher-manual/current/syntax/operators/#query-syntax-string-predicates)
- See also: [H1](cypher-generation-issues.md#h1-string-methods-use--posix-regex-instead-of-native-operators)

---

## F-08: Cypher Query Details Leaked via Debug Logging

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeCypherEngine.cs:92`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:92), [`AgeNodeManager.cs:142`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:142), [`QueryParameterStore.cs:41`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:41) |
| **Status** | ✅ Resolved — parameter values moved to Trace level; Debug logs only name and type |

### Description

Complete Cypher queries and their parameter values are logged at `LogLevel.Debug` across multiple components:

```csharp
// AgeCypherEngine.cs:92
_logger.LogDebug("Executing Cypher query: {Cypher}", cypher);

// AgeNodeManager.cs:142
_logger.LogDebug("Setting property {PropertyName} to {Value}", propertyName, value);

// QueryParameterStore.cs:41
_logger.LogDebug("Added parameter {ParameterName} = {ParameterValue}", name, value);
```

At `Debug` log level, these details are visible in production logging systems if the log level is configured to `Debug` or higher. This can leak sensitive data embedded in queries or parameter values.

### Impact

- **Information disclosure**: Query text may reveal database schema, relationship types, and property names. Parameter values may contain [PII (Personally Identifiable Information)](https://en.wikipedia.org/wiki/Personal_data), secrets, or other sensitive data.
- **Compliance**: Logging PII may violate GDPR, HIPAA, or other data protection regulations.
- **Security audit exposure**: Full query logs provide attackers with detailed knowledge of the data model and query patterns.

### Recommended Fix

Redact parameter values in `Debug` log output. Move full query and parameter value output to `LogLevel.Trace`. Use parameter type information in `Debug` logs instead of values.

### Implementation Options

- **Option A** (Recommended): Log parameter types instead of values at `Debug`; move full output to `Trace`
  - Change `Debug` logging to: `_logger.LogDebug("Added parameter {ParameterName} ({ParameterType})", name, value?.GetType().Name)`
  - Move full query and value logging to `LogLevel.Trace`
  - Configure `Trace` level only in development environments

- **Option B**: Add a configurable redaction delegate
  - Add a `Func<string, string?>? parameterRedactor` callback to the configuration
  - When set, the callback is invoked on parameter values before logging
  - This gives deployers control over what is logged

### Acceptance Criteria

- [ ] `Debug` log output no longer contains raw parameter values
- [ ] Full query details are moved to `Trace` level
- [ ] Documentation notes the logging behavior and how to enable full detail for debugging
- [ ] Tests verify the log output at each level

### References

- [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)
- [CWE-532: Insertion of Sensitive Information into Log File](https://cwe.mitre.org/data/definitions/532.html)

---

## F-09: Raw Exception Messages Propagate to Callers

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeCypherEngine.cs:127-131`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:127-131) |
| **Status** | ✅ Resolved — NpgsqlException caught and rethrown as sanitized GraphException with correlation ID |

### Description

Exceptions thrown during Cypher execution are re-thrown without sanitization. The raw exception messages — which may include PostgreSQL server details, query text, stack traces, and internal state — propagate directly to callers of the library.

```csharp
// Simplified pattern
catch (Exception ex)
{
    // No sanitization
    throw;
}
```

### Impact

- **Information disclosure**: Exception messages may contain server version numbers, database paths, query text, and other internal details useful for attack planning.
- **Security through obscurity violation**: Server details that should remain internal are exposed to potentially untrusted callers.
- **Compliance risk**: Detailed error messages may contain sensitive data logged by consuming applications.

### Exploitation Scenario

A LINQ query with a syntax error causes PostgreSQL to return an error like:

```
Npgsql.PostgresException (0x80004005): ERROR: relation "ag_catalog.ag_graph" does not exist
POSITION: 15
```

If this exception propagates unmodified to an HTTP API response, an attacker learns that the database is PostgreSQL with AGE extension, the internal schema name, and possibly the server version.

### Recommended Fix

Wrap exceptions in a sanitized [`GraphException`](../../../src/Graph.Model.Age/Core/Exceptions/GraphException.cs) (or equivalent) that removes or redacts server-specific details before propagation.

### Implementation Options

- **Option A**: Create a [`GraphException`](https://learn.microsoft.com/en-us/dotnet/api/system.exception) wrapper that strips server details
  - Catch Npgsql-specific exceptions and re-throw as `GraphException` with a sanitized message
  - Include the original exception type and a correlation ID in the new exception
  - Log the full exception details internally at `Error` level

- **Option B**: Add exception filtering middleware pattern
  - Create a configurable exception handler that can be plugged into the execution pipeline
  - Allow the deployer to choose between raw and sanitized exceptions
  - Default to sanitized

### Acceptance Criteria

- [ ] Raw PostgreSQL exception messages no longer propagate directly to callers
- [ ] A sanitized exception wrapper is used for all database-originated exceptions
- [ ] Full exception details are logged internally at `Error` level with a correlation ID
- [ ] The sanitized message is user-actionable (e.g., "Query execution failed — see internal log for details")

### References

- [CWE-209: Generation of Error Message Containing Sensitive Information](https://cwe.mitre.org/data/definitions/209.html)
- [OWASP Error Handling Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Error_Handling_Cheat_Sheet.html)

---

## F-10: No Expression Tree Depth or Complexity Limits

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeExpressionToCypherVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs) (entire file) |
| **Status** | ✅ Resolved — added node count limit (10,000) and recursion depth limit (100) |

### Description

The [`AgeExpressionToCypherVisitor`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs) performs recursive descent traversal of expression trees without any limits on depth or total node count. A deeply nested or extremely large expression tree can cause:

- **Stack overflow**: Deeply nested expressions (e.g., `a && b && c && ...`) cause stack overflow when the recursion depth exceeds the call stack limit.
- **CPU exhaustion**: Expression trees with millions of nodes (e.g., `Combine(Combine(Combine(...)))`) consume excessive CPU time for traversal.
- **Denial of service**: Malicious or accidentally malformed expressions can crash or hang the application.

### Impact

- **Denial of service (DoS)**: An attacker can craft an expression that causes stack overflow or CPU exhaustion during query translation.
- **Application crash**: Stack overflow terminates the process without recovery.
- **Resource exhaustion**: Long traversal times block threads in the thread pool, reducing application throughput.

### Exploitation Scenario

If the application accepts LINQ expressions from external sources (e.g., a query API that accepts `Expression<Func<T, bool>>`), an attacker could submit:

```csharp
x => true && true && true && ... // 100,000 nested && operations
```

This would cause the recursive visitor to deeply recurse, overflowing the call stack and crashing the process.

### Recommended Fix

Add a recursion depth counter and a maximum node count to the visitor. Abort translation with a clear error when limits are exceeded.

### Implementation Options

- **Option A**: Add `_visitedNodeCount` with `MaxNodeCount = 10,000`
  - Increment a counter on every node visit
  - Throw [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception) when `MaxNodeCount` is exceeded
  - Reset the counter per query translation

- **Option B**: Also add recursion depth limit of 100
  - Track current recursion depth alongside node count
  - Throw when depth exceeds 100
  - Detects deeply nested expressions early, before node count limit is reached

### Acceptance Criteria

- [ ] A maximum node count (e.g., 10,000) is enforced during expression tree traversal
- [ ] A maximum recursion depth (e.g., 100) is enforced
- [ ] Exceeding limits throws a descriptive exception
- [ ] Tests confirm that deep/large expressions are rejected with the expected exception

### References

- [CWE-674: Uncontrolled Recursion](https://cwe.mitre.org/data/definitions/674.html)
- [CWE-770: Allocation of Resources Without Limits or Throttling](https://cwe.mitre.org/data/definitions/770.html)

---

## F-11: AgeSerializationBridge.FromAgeValue Loose Boolean/String Parsing

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeSerializationBridge.cs:102-106`](../../../src/Graph.Model.Age/Core/Entities/AgeSerializationBridge.cs:102-106) |
| **Status** | ✅ Resolved — strict bool.TryParse() used instead of loose "1"/"0" parsing |

### Description

The [`FromAgeValue`](../../../src/Graph.Model.Age/Core/Entities/AgeSerializationBridge.cs:102-106) method in [`AgeSerializationBridge`](../../../src/Graph.Model.Age/Core/Entities/AgeSerializationBridge.cs) accepts both `"1"/"0"` and `"true"/"false"` as valid boolean representations. This loose parsing can mask data integrity issues and potentially cause unexpected behavior when the input is not a clean boolean value.

```csharp
// Simplified: accepts "1"/"0" in addition to "true"/"false"
if (value is string sv && (sv == "1" || sv == "true")) return true;
if (value is string sv && (sv == "0" || sv == "false")) return false;
```

### Impact

- **Data integrity ambiguity**: Values that logically should be `"true"` or `"false"` might be stored as `"1"` or `"0"`, and vice versa. The lenient parsing could conceal serialization issues.
- **Unexpected deserialization**: If AGE returns `"1"` for a boolean property (which it does in some internal representations), the deserialization succeeds but the round-trip may not be stable.
- **Edge case confusion**: Other truthy/falsy values (e.g., `"yes"/"no"`, `"on"/"off"`) are not accepted, creating inconsistency.

### Exploitation Scenario

While not directly exploitable for injection, loose parsing could enable subtle data corruption. For example, if a property is inadvertently stored as the string `"1"` instead of `true`, the deserialization will produce `true`, but writing it back might produce a different representation, causing unnecessary diffs in the database.

### Recommended Fix

Accept only `"true"` and `"false"` (case-insensitive) for boolean deserialization from string values. Reject or fail for `"1"`/`"0"` representations.

### Implementation Options

- **Option A** (Recommended): Strict parsing — only accept `"true"`/`"false"`
  - Use `bool.TryParse(value, out result)` which only accepts `"true"`/`"false"` (case-insensitive)
  - Throw [`InvalidCastException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidcastexception) for unrecognized values

- **Option B**: Accept `"1"`/`"0"` but log a warning for migration
  - Log a [`Warning`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel) when `"1"`/`"0"` is encountered
  - Add a configuration option to enable/disable lenient parsing
  - Plan removal of lenient parsing in a future major version

### Acceptance Criteria

- [ ] Boolean deserialization uses strict `"true"`/`"false"` parsing by default
- [ ] Invalid boolean strings throw a descriptive exception
- [ ] Tests confirm both valid and invalid inputs

### References

- [CWE-1725: Inadequate Parsing of Unexpected Data](https://cwe.mitre.org/data/definitions/1725.html)

---

## F-12: Parameter Values Logged at Debug Level

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`QueryParameterStore.cs:41`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:41) |
| **Status** | ✅ Resolved — parameter values moved to Trace; Debug logs parameter name and type only |

### Description

In [`QueryParameterStore.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs), parameter values are logged at `Debug` level when they are added to the parameter store:

```csharp
_logger.LogDebug("Added parameter {ParameterName} = {ParameterValue}", name, value);
```

While this provides useful debugging information, the parameter values may contain [PII (Personally Identifiable Information)](https://en.wikipedia.org/wiki/Personal_data), secrets, or other sensitive data that should not appear in production logs.

### Impact

- **Low-severity information disclosure**: Parameter values may leak in environments where `Debug` logging is enabled.
- **Compliance risk**: If parameter values contain PII or sensitive business data, logging them may violate GDPR, HIPAA, or internal data policies.

### Recommended Fix

Log only the parameter name and type at `Debug` level, moving the full parameter value to `Trace` level.

### Implementation Options

- **Option A**: `_logger.LogDebug("Added parameter {ParameterName} ({ParameterType})", name, value?.GetType().Name)`
  - Log parameter name and type at `Debug`
  - Log name and value at `Trace`

- **Option B**: Add opt-in full value logging configuration
  - Add a `bool LogParameterValues` option to [`AgeCypherQueryConfiguration`](../../../src/Graph.Model.Age/Core/AgeCypherQueryConfiguration.cs)
  - When `false` (default), log only name and type
  - When `true`, log name and value (for development use)

### Acceptance Criteria

- [ ] `Debug` logging no longer includes raw parameter values
- [ ] A configuration option is available to enable full value logging
- [ ] Documentation notes the logging behavior

### References

- [CWE-532: Insertion of Sensitive Information into Log File](https://cwe.mitre.org/data/definitions/532.html)

---

## F-13: Static ConcurrentDictionary Holds Transaction References Indefinitely

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`AgeGraphTransaction.cs:30-31`](../../../src/Graph.Model.Age/Core/AgeGraphTransaction.cs:30-31) |
| **Status** | ✅ Resolved — ConcurrentDictionary replaced with ConditionalWeakTable for auto-cleanup |

### Description

A static [`ConcurrentDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2) in [`AgeGraphTransaction.cs`](../../../src/Graph.Model.Age/Core/AgeGraphTransaction.cs) holds strong references to all active transactions indefinitely:

```csharp
private static readonly ConcurrentDictionary<string, AgeGraphTransaction> _activeTransactions = new();
```

Transactions are never removed from this dictionary (no cleanup mechanism). This causes:

- **Memory leak**: Long-running applications accumulate transaction objects, never releasing them for garbage collection.
- **Resource exhaustion**: Each transaction may hold database connections, network sockets, or other unmanaged resources that are never released until process termination.

### Impact

- **Memory exhaustion**: Over time, the dictionary grows without bound, consuming increasing amounts of memory.
- **Resource leak**: Database connections and other resources held by transaction objects are never released.
- **Application outage**: Eventually, the application may run out of memory or database connections, causing service disruption.

### Exploitation Scenario

An attacker who can trigger transaction creation (e.g., by issuing many API requests that each open a transaction) can cause the application to accumulate transactions in the static dictionary indefinitely. Each transaction adds an entry that is never removed, leading to eventual resource exhaustion.

### Recommended Fix

Replace the static dictionary with a [`ConditionalWeakTable<TKey, TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2) or add periodic cleanup to remove stale entries.

### Implementation Options

- **Option A** (Recommended): Use [`ConditionalWeakTable<string, AgeGraphTransaction>`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2) instead of [`ConcurrentDictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)
  - `ConditionalWeakTable` holds weak references to keys, allowing GC to reclaim entries when the key is no longer referenced elsewhere
  - Requires a key object rather than a string; use a dedicated key class or wrap the transaction ID

- **Option B**: Add timestamp-based stale entry cleanup
  - Store a timestamp with each transaction entry
  - Add a background or periodic cleanup that removes entries older than a configurable threshold
  - Call cleanup on every dictionary access

### Acceptance Criteria

- [ ] The static dictionary no longer holds strong references that prevent GC
- [ ] Memory profiling confirms that completed transactions are reclaimable
- [ ] Transaction lifecycle tests verify proper cleanup

### References

- [.NET Memory Leak: Static Dictionaries](https://michaelscodingspot.com/ways-to-cause-memory-leaks-in-dotnet/)
- [ConditionalWeakTable Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2)

---

## F-14: Dependency Versions Should Be Monitored for CVEs

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Security |
| **Source Review** | Security Audit |
| **Affected File(s)** | [`Graph.Model.Age.csproj:22-23`](../../../src/Graph.Model.Age/Graph.Model.Age.csproj:22-23) |
| **Status** | ✅ Resolved — created .github/dependabot.yml for weekly NuGet vulnerability scanning |

### Description

The project depends on:

- [`Npgsql`](https://www.nuget.org/packages/Npgsql/) 10.0.3
- [`Konnektr.Npgsql.Age`](https://www.nuget.org/packages/Konnektr.Npgsql.Age/) 2.0.0

There is no automated dependency vulnerability scanning configured in the CI pipeline. New [CVEs (Common Vulnerabilities and Exposures)](https://cve.mitre.org/) are discovered regularly for NuGet packages, and without automated monitoring, the project may remain vulnerable to known exploits until a manual audit discovers the issue.

### Impact

- **Delayed vulnerability response**: Without automated scanning, the team may not learn about a CVE affecting Npgsql or its transitive dependencies until weeks or months after disclosure.
- **Supply chain risk**: The dependency tree may contain vulnerable transitive packages that go unnoticed.

### Recommended Fix

Add automated dependency vulnerability scanning to the CI pipeline. Use tools like [Dependabot](https://docs.github.com/en/code-security/dependabot), [`dotnet list package --vulnerable`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package), or [GitHub Advisory Database](https://github.com/advisories).

### Implementation Options

- **Option A**: Add Dependabot configuration to `.github/`
  - Create `.github/dependabot.yml` with NuGet ecosystem monitoring
  - Configure weekly checks for version updates
  - Enable security alerts for vulnerable dependencies

- **Option B**: Add a [`dotnet list package --vulnerable`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package) step to CI
  - Add a build step: `dotnet list package --vulnerable --include-transitive 2>&1`
  - Fail the build if any vulnerable packages are detected
  - This is a lightweight option that doesn't require external services

### Acceptance Criteria

- [ ] Automated vulnerability scanning is configured in the CI pipeline
- [ ] The scanning covers direct and transitive dependencies
- [ ] A process is documented for responding to vulnerability alerts

### References

- [GitHub Dependabot Configuration](https://docs.github.com/en/code-security/dependabot/dependabot-version-updates/configuring-dependabot-version-updates)
- [.NET Vulnerability Scanning](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package#output-format)
- [OWASP Dependency Check](https://owasp.org/www-project-dependency-check/)

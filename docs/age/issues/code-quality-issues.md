# Code Quality Issues

> Code quality findings from the AGE provider review. All 23 issues are documented below with severity ratings, impact analysis, and recommended fixes.

---

## CRIT-1: Reflection-Based DynamicInvoke with Memory Leak Risk

| Field | Value |
|-------|-------|
| **Severity** | 🔴 Critical |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeCypherEngine.cs:181-202`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:181-202) |
| **Status** | ✅ Resolved — replaced DynamicInvoke with cached expression tree delegates |

### Description

The [`AgeCypherEngine`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs) uses a pattern involving [`MakeGenericMethod`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo.makegenericmethod), [`MethodInfo.Invoke`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodbase.invoke), and [`DynamicInvoke`](https://learn.microsoft.com/en-us/dotnet/api/system.delegate.dynamicinvoke) combined with [`Task.Result`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1.result) to execute queries:

```csharp
// Simplified pattern at lines 181-202
var method = typeof(AgeCypherEngine)
    .GetMethod(nameof(ExecuteAsync), BindingFlags...)
    .MakeGenericMethod(resultType);
var task = (Task)method.Invoke(this, new object[] { query, ... });
task.Wait(); // Or Task.Result
```

This pattern has multiple problems:
- [`DynamicInvoke`](https://learn.microsoft.com/en-us/dotnet/api/system.delegate.dynamicinvoke) is extremely slow (boxing, argument array allocation, type checking)
- [`MakeGenericMethod`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo.makegenericmethod) + [`Invoke`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodbase.invoke) bypasses compile-time type safety
- [`Task.Result`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1.result) or [`Wait()`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.wait) can cause deadlocks in synchronization contexts
- Dynamic invocation delegates are not cached, causing repeated reflection overhead and memory allocations

### Impact

- **Performance degradation**: `DynamicInvoke` is ~20-50x slower than a direct delegate call.
- **Memory pressure**: Each call allocates new arrays and boxed objects.
- **Deadlock risk**: `Task.Result` in a synchronization context (e.g., ASP.NET) can cause deadlocks.
- **Maintenance burden**: Reflection-heavy code is difficult to debug and refactor.

### Recommended Fix

Replace the reflection pattern with cached [`Func<...>`](https://learn.microsoft.com/en-us/dotnet/api/system.func-2) delegates, eliminating per-call reflection overhead.

### Implementation Options

- **Option A**: [`ConcurrentDictionary<Type, Delegate>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2) with compiled expression trees
  - Cache generic method specializations keyed by `Type`
  - Use [`Expression.Lambda`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lambda) to create and compile delegates
  - This provides type-safe, cached, fast invocation

- **Option B**: Introduce [`IQueryProvider.ExecuteAsync<TResult>`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.iqueryprovider.executeasync-1) pattern
  - Define an interface with a typed `ExecuteAsync<TResult>` method
  - Use the interface for type-safe invocation without reflection
  - This is a more architectural change but eliminates reflection entirely

### Acceptance Criteria

- [ ] `DynamicInvoke` is no longer used in the codebase
- [ ] Query execution uses cached delegates or an interface-based dispatch
- [ ] Performance benchmark shows improvement over the reflection-based approach
- [ ] No deadlock risk from `Task.Result` or `.Wait()` in the execution path

### References

- [DynamicInvoke Performance (MSDN blog)](https://learn.microsoft.com/en-us/archive/blogs/csharpfaq/why-is-dynamicinvoke-so-slow)
- [Avoid Task.Result and Task.Wait](https://learn.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development)

---

## CRIT-2: Expression Recompilation Without Caching in Hot Paths

| Field | Value |
|-------|-------|
| **Severity** | 🔴 Critical |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeExpressionToCypherVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs), [`MemberExpressionHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs), [`StringMethodHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) (6+ locations) |
| **Status** | ✅ Resolved — added ConditionalWeakTable cache and pre-compiled delegate caching |

### Description

`Expression.Lambda<Func<object>>(...).Compile()` is called repeatedly without caching in multiple visitor components. Each call to [`Compile()`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile) generates new IL code at runtime, which is CPU-intensive and allocates memory. When the same expression (or structurally identical expression) is encountered multiple times, the compilation is repeated unnecessarily.

### Impact

- **CPU overhead**: Expression compilation is expensive; repeated compilation of equivalent expressions wastes CPU time.
- **Memory pressure**: Each compiled delegate and associated generated code occupies memory; without caching, this memory is duplicated.
- **Query translation latency**: Hot paths that compile expressions on every translation exhibit higher latency.
- **GC pressure**: Compiled delegates and intermediate allocations add GC overhead.

### Recommended Fix

Cache compiled delegates in a [`ConcurrentDictionary<Expression, object?>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2) keyed by expression reference or by a structural hash.

### Implementation Options

- **Option A**: Add a per-instance cache dictionary with weak references
  - Use [`ConditionalWeakTable<Expression, object?>`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2) or a similar weak-reference cache
  - This allows cached values to be garbage collected when expressions are no longer in use
  - Key by expression reference identity (not structural equality)

- **Option B**: Prefer direct [`ConstantExpression`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.constantexpression) pattern matching first, compile as last resort
  - Add pattern matching for `ConstantExpression` and `MemberExpression` on constants before falling back to compilation
  - Many expression tree patterns can be resolved without compilation
  - Reserve `Compile()` only for truly dynamic cases

### Acceptance Criteria

- [ ] Expression compilation is cached; equivalent expressions are not recompiled
- [ ] Cache size is bounded to prevent unbounded memory growth
- [ ] Tests confirm that repeated expression translations hit the cache
- [ ] Performance benchmark shows reduction in query translation time

### References

- [Expression Tree Compilation Performance](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/#compiling-expression-trees)

---

## CRIT-3: AggregationDetector and QueryExpressionAnalyzer Are Untested Static Analysis

| Field | Value |
|-------|-------|
| **Severity** | 🔴 Critical |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AggregationDetector.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AggregationDetector.cs), [`QueryExpressionAnalyzer.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryExpressionAnalyzer.cs) |
| **Status** | ✅ Resolved — magic strings replaced with AggregationKind enum and MethodInfo-based detection |

### Description

[`AggregationDetector`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AggregationDetector.cs) and [`QueryExpressionAnalyzer`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryExpressionAnalyzer.cs) perform static analysis of expression trees using magic strings to identify method calls:

```csharp
// Example: magic string matching
if (methodCall.Method.Name == "ToDictionary" || methodCall.Method.Name == "Single" || ...)
```

This approach is fragile because:
- Magic strings like `"ToDictionary"`, `"Single"`, `"Average"` will break under [obfuscation](https://learn.microsoft.com/en-us/dotnet/standard/assembly/obfuscation) or [AOT (Ahead-of-Time) compilation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) scenarios
- No unit tests exist for the detection logic
- Changes to LINQ provider method names could silently break detection
- The detection logic is scattered across files without a central definition

### Impact

- **Maintenance burden**: Magic strings make the code fragile and hard to refactor.
- **Test gap**: Untested detection logic may produce incorrect results for edge cases.
- **AOT incompatibility**: String-based method detection may not work with Native AOT compilation.
- **Silent failures**: Incorrect detection leads to wrong Cypher generation without errors.

### Recommended Fix

Replace magic strings with an [`enum`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum) or method-info-based comparison, and add comprehensive unit tests.

### Implementation Options

- **Option A**: Define an [`AggregationKind`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum) enum, add dedicated tests for each value
  - Create `enum AggregationKind { Sum, Average, Min, Max, Count, ToDictionary, Single, ... }`
  - Map each aggregation to a method info comparison: `typeof(Enumerable).GetMethod(nameof(Enumerable.Sum), ...)`
  - Add a dedicated test class with tests for each aggregation kind

- **Option B**: Use method info comparison instead of string matching
  - Store static `MethodInfo` references for known aggregation methods
  - Compare using `ReferenceEquals` or `MethodInfo.Equals`
  - This preserves compile-time checking through method references

### Acceptance Criteria

- [ ] Magic strings are replaced with enum or method-info-based detection
- [ ] Unit tests cover each aggregation kind (Sum, Average, Min, Max, Count, etc.)
- [ ] Tests verify both positive detection and negative (non-aggregation) cases
- [ ] Detection works correctly under obfuscation/AOT scenarios

### References

- [MethodInfo Equality](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo.equals)

---

## HIGH-1: ConfigureAwait(false) Inconsistency

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeCypherEngine.cs:61-132`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:61-132) |
| **Status** | ✅ Resolved — all await calls now use .ConfigureAwait(false) |

### Description

In [`ExecuteAsync<T>`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs), some [`await`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/await) calls use [`.ConfigureAwait(false)`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.configureawait) while others do not. In a library context (as opposed to application code), `ConfigureAwait(false)` should be used consistently to avoid forcing continuations back to the original [`SynchronizationContext`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.synchronizationcontext).

### Impact

- **Deadlock risk**: In UI or ASP.NET Classic contexts, missing `ConfigureAwait(false)` can cause deadlocks when `Task.Result` or `.Wait()` is called on async methods.
- **Performance overhead**: Unnecessary continuation marshaling to the original context adds overhead.
- **Inconsistent behavior**: Some async paths yield back to the original context while others don't, leading to unpredictable behavior.

### Recommended Fix

Add `.ConfigureAwait(false)` to every `await` call in [`ExecuteAsync<T>`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs) consistently.

### Implementation Options

- **Option A**: Add `.ConfigureAwait(false)` to every `await` in the method
  - Add a using directive: `using System.Threading.Tasks;`
  - Append `.ConfigureAwait(false)` to each `await` expression
  - Verify no `await` is missed

- **Option B**: Add a [Roslyn analyzer](https://learn.microsoft.com/en-us/dotnet/roslyn/analyzers/) rule to enforce this
  - Add `CA2007: Consider calling ConfigureAwait on the awaited task` as an error
  - Add an `.editorconfig` entry: `dotnet_diagnostic.CA2007.severity = error`
  - This prevents future regressions

### Acceptance Criteria

- [ ] All `await` calls in [`AgeCypherEngine.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs) use `.ConfigureAwait(false)`
- [ ] CA2007 is configured as an error in the project's `.editorconfig` or [`GlobalSuppressions.cs`](../../../tests/Graph.Model.Performance.Tests/GlobalSuppressions.cs)
- [ ] No deadlock risk from sync-over-async patterns

### References

- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
- [CA2007: Consider calling ConfigureAwait on the awaited task](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007)

---

## HIGH-2: Broad `catch { }` Swallowing Exceptions

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`ProjectionFragmentVisitor.cs:226-228,242`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/ProjectionFragmentVisitor.cs:226-228) |
| **Status** | ✅ Resolved — silent catch replaced with logged warning and InvalidOperationException rethrow |

### Description

In [`ProjectionFragmentVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/ProjectionFragmentVisitor.cs), a broad `catch { }` swallows exceptions silently and returns a fallback value:

```csharp
try
{
    // ... fragment processing
}
catch
{
    // Silent catch produces "unknown" as valid Cypher
    return "unknown";
}
```

The fallback value `"unknown"` is syntactically valid Cypher but semantically meaningless. The original error is completely lost, making debugging extremely difficult.

### Impact

- **Silent data corruption**: Instead of failing loudly, malformed projections produce garbage Cypher that may execute successfully but return wrong results.
- **Debugging nightmare**: Developers receive no indication that a projection failed; they only see incorrect query results.
- **Lost diagnostic information**: The original exception type, message, and stack trace are discarded entirely.

### Recommended Fix

Log the exception at `Warning` level before the fallback, and consider rethrowing with a descriptive message rather than silently returning garbage Cypher.

### Implementation Options

- **Option A**: Log + throw descriptive [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception)
  - Log the exception at `LogLevel.Warning`
  - Throw `InvalidOperationException` with context about which fragment failed
  - This fails loudly and provides diagnostic information

- **Option B**: Log + return a marker that triggers a clear error at render time
  - Return a dedicated marker type (e.g., `FragmentError`) instead of `"unknown"`
  - The renderer detects the marker and produces an invalid Cypher that yields a clear error at execution time
  - This ensures the error is surfaced at query execution rather than silently ignored

### Acceptance Criteria

- [ ] The silent `catch { }` no longer exists in [`ProjectionFragmentVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/ProjectionFragmentVisitor.cs)
- [ ] Exceptions are logged at minimum `Warning` level
- [ ] The fallback behavior is documented and auditable
- [ ] Tests verify that projection failures are surfaced correctly

### References

- [CWE-755: Improper Handling of Exceptional Conditions](https://cwe.mitre.org/data/definitions/755.html)

---

## HIGH-3: Thread Safety Violation in CypherQueryScope

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`CypherQueryScope.cs:44-45`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryScope.cs:44-45) |
| **Status** | ✅ Resolved — added XML documentation noting the type is NOT thread-safe; must be per-query instance |

### Description

[`CypherQueryScope`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryScope.cs) contains mutable state (`hopAliases`, `hopTypes`, `CurrentHop`) that is modified during expression tree traversal without any thread-safety guards. If multiple queries are being translated concurrently (which is common in web applications), these mutable fields can experience race conditions.

```csharp
// Lines 44-45: Mutable state without synchronization
public int CurrentHop { get; set; }
internal Dictionary<string, int> hopAliases = new();
internal Dictionary<string, HopType> hopTypes = new();
```

### Impact

- **Data races**: Concurrent query translation can corrupt scope state, leading to incorrect Cypher generation.
- **Hard-to-reproduce bugs**: Race conditions are intermittent and environment-dependent.
- **Query corruption**: Corrupted scope state can produce invalid or wrong Cypher queries.

### Recommended Fix

Add explicit thread-safety documentation or locking around state mutations. Since each query translation should have its own scope, consider using a per-query scope instance rather than sharing state.

### Implementation Options

- **Option A**: Add [`[ThreadSafety(false)]`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadsafetyattribute) attribute and document
  - Add the attribute to document that the type is not thread-safe
  - Ensure the type is used as a per-query instance (not shared across translations)
  - Add a code comment noting that callers must not share scope across threads

- **Option B**: Add [`lock`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock) around state mutations
  - Add a private readonly object for locking
  - Guard all property getters/setters and dictionary mutations with `lock`
  - This ensures thread safety but adds overhead

### Acceptance Criteria

- [ ] Thread safety guarantees are clearly documented for [`CypherQueryScope`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryScope.cs)
- [ ] The type is either made thread-safe or explicitly documented as not thread-safe
- [ ] All usages respect the thread-safety contract
- [ ] Concurrent query translation tests do not produce corrupted scope state

### References

- [Thread Safety in .NET](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices)

---

## HIGH-4: StringMethodHandler — Three Nearly Identical Methods

| Field | Value |
|-------|-------|
| **Severity** | 🟠 High |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`StringMethodHandler.cs:81-164`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:81-164) |
| **Status** | ✅ Resolved — common logic extracted into BuildStringMatchExpression; ~75 lines of duplication eliminated |

### Description

[`HandleStringContains`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs), [`HandleStringStartsWith`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs), and [`HandleStringEndsWith`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) are structurally identical. Only the regex pattern used for the `=~` operator differs between them:

- `HandleStringContains` → `'.*' + pattern + '.*'`
- `HandleStringStartsWith` → `'^' + pattern + '.*'`
- `HandleStringEndsWith` → `'.*' + pattern + '$'`

Other logic — parameter extraction, fallback handling, null checking — is duplicated across all three methods.

### Impact

- **Code duplication**: ~250 lines of near-identical code across the three methods.
- **Maintenance burden**: Any change to the common logic must be applied three times.
- **Bug propagation**: A bug fixed in one method may be missed in the others.

### Recommended Fix

Extract the common pattern into a single helper method with a strategy parameter for the wrapping logic.

### Implementation Options

- **Option A**: Create `HandleRegexMatch(string? obj, Expression arg, Func<string, string> wrapPattern)`
  - Extract common logic (parameter extraction, null handling, fallback) into a single method
  - Each handler method calls the common method with a different `wrapPattern` delegate

- **Option B**: Use a strategy pattern with a lookup dictionary
  - Create a dictionary mapping `StringMethodKind` to a pattern strategy
  - The common handler selects the strategy from the dictionary
  - This is more flexible and extensible

### Acceptance Criteria

- [ ] The three string handler methods share a common implementation
- [ ] Each handler is a thin wrapper that specifies only the unique pattern logic
- [ ] All existing tests continue to pass
- [ ] No regression in string method Cypher generation

### References

- [DRY (Don't Repeat Yourself) Principle](https://en.wikipedia.org/wiki/Don%27t_repeat_yourself)

---

## MED-1: AgeFragmentRenderer.Render — Method Complexity

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeFragmentRenderer.cs:55-205`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:55-205) |
| **Status** | ✅ Resolved — Render method decomposed into 5 focused sub-methods |

### Description

The [`Render`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs) method in [`AgeFragmentRenderer`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs) spans approximately 150 lines with deep conditional nesting (5+ levels). It handles multiple rendering modes (`Simple`, `WithWithClause`, etc.), sorting, ordering, and pagination in a single monolithic method.

### Impact

- **Readability**: Deep nesting and length make the method hard to read and understand.
- **Testability**: A monolithic method is difficult to unit test comprehensively.
- **Maintainability**: Adding new rendering modes or modifying existing ones risks introducing regressions.

### Recommended Fix

Decompose the monolithic method into smaller, focused methods, each with a single responsibility.

### Implementation Options

- **Option A**: Extract 3-4 focused methods
  - Create `RenderWithWithClause`, `RenderSimple`, `AppendOrderBy`, `AppendSkipLimit`
  - Each method has a clear, single responsibility
  - The main `Render` method becomes a dispatcher

- **Option B**: Introduce a builder pattern for the rendering pipeline
  - Create a `CypherQueryBuilder` class with chainable methods
  - Each rendering step (WITH clause, ORDER BY, SKIP/LIMIT) is a separate builder method
  - This produces more testable and extensible code

### Acceptance Criteria

- [ ] The `Render` method is decomposed into focused sub-methods
- [ ] Each sub-method has a single responsibility
- [ ] All existing tests continue to pass
- [ ] Code complexity metrics (e.g., cyclomatic complexity) are reduced

### References

- [Single Responsibility Principle](https://en.wikipedia.org/wiki/Single-responsibility_principle)
- [Cyclomatic Complexity](https://en.wikipedia.org/wiki/Cyclomatic_complexity)

---

## MED-2: TryHandleStaticDateTime — Dead Code Path

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`MemberExpressionHandler.cs:137-171`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs:137-171) |
| **Status** | ✅ Resolved — consolidated identical if-else branches into single code path |

### Description

In [`TryHandleStaticDateTime`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs), both branches of a conditional structure appear to execute the same logic:

```csharp
if (condition)
{
    // Branch A: some logic
}
else
{
    // Branch B: identical logic
}
```

Both branches produce the same result, making the conditional effectively dead code. This could be:
- A copy-paste error where one branch was intended to do something different
- Leftover code from a refactoring that was never cleaned up

### Impact

- **Confusion**: Developers reading the code wonder what the intended difference was.
- **Dead code**: The conditional adds unnecessary complexity.
- **Potential bug**: If one branch was supposed to handle a different case, the bug is masked.

### Recommended Fix

Consolidate the two branches into a single code path, removing the unnecessary conditional.

### Implementation Options

- **Option A**: Consolidate into single path
  - Remove the `if-else` entirely and keep the common code
  - Add a comment noting the consolidation if there's historical context

- **Option B**: Add explicit comment if there's a semantic difference
  - If the two branches are intentionally identical (e.g., for future extensibility), add a clear comment explaining why
  - Otherwise, consolidate

### Acceptance Criteria

- [ ] The `if-else` is removed and replaced with a single code path
- [ ] Behavior is unchanged (all tests pass)
- [ ] If there's an intentional reason for the duplication, it's documented with a code comment

### References

- [Code Smell: Dead Code](https://refactoring.guru/smells/dead-code)

---

## MED-3: QueryParameterStore — O(n) Lookup With Equals on Unconstrained Values

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`QueryParameterStore.cs:30-37`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:30-37) |
| **Status** | ✅ Resolved — O(n) scan replaced with O(1) Dictionary lookup using EqualityComparer.Default |

### Description

[`QueryParameterStore`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs) performs an O(n) linear scan on every [`Add()`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.icollection-1.add) call to deduplicate parameters:

```csharp
// Linear scan: O(n) per add
if (!_parameters.Any(p => Equals(p.Value, value)))
{
    _parameters.Add(new QueryParameter(name, value));
}
```

Since `value` is `object?`, [`Equals`](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals) dispatches to the runtime type's `Equals` implementation, which can:
- Throw for types that don't implement `Equals` properly
- Be expensive for complex types
- Behave unexpectedly for custom types

### Impact

- **Performance degradation**: O(n²) behavior when adding many parameters.
- **Unpredictable Equals behavior**: Custom types may throw or produce incorrect equality results.
- **Hidden overhead**: Each query translation pays this O(n) cost.

### Recommended Fix

Use a [`Dictionary<object?, string>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2) with a custom comparer for O(1) deduplication.

### Implementation Options

- **Option A**: Use [`Dictionary`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2) with [`StructuralEqualityComparer`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.structuralcomparisons.structuralequalitycomparer)
  - Maintain both a list (for ordering) and a dictionary (for dedup)
  - O(1) dedup lookup

- **Option B**: Skip dedup entirely (PostgreSQL caches anyway)
  - PostgreSQL's query plan cache handles repeated identical queries efficiently
  - Removing dedup eliminates the O(n) scan and the `Equals` risk
  - Simpler code at the cost of slightly larger query text

### Acceptance Criteria

- [ ] Parameter deduplication is O(1) or removed entirely
- [ ] No [`Equals`](https://learn.microsoft.com/en-us/dotnet/api/system.object.equals) is called on unconstrained `object?` values
- [ ] All existing tests continue to pass

### References

- [Dictionary<TKey, TValue> Class](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2)

---

## MED-4: GetComplexProperties — Reflection on Every Query

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`QueryInitializationHandler.cs:164-174`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryInitializationHandler.cs:164-174) |
| **Status** | ✅ Resolved — results cached per type via ConcurrentDictionary.GetOrAdd |

### Description

[`GetComplexProperties`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryInitializationHandler.cs) calls [`GetProperties()`](https://learn.microsoft.com/en-us/dotnet/api/system.type.getproperties), [`Where`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.where), and [`ToList`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.tolist) on every query execution:

```csharp
// Called on every query — no caching
var complexProps = typeof(TEntity)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Where(p => IsComplexType(p.PropertyType))
    .ToList();
```

This reflection overhead is unnecessary for types whose structure doesn't change at runtime (the common case).

### Impact

- **Performance overhead**: Reflection on every query adds measurable latency.
- **GC pressure**: Each call creates intermediate arrays and list allocations.
- **CPU waste**: Repeated reflection on the same types is wasted computation.

### Recommended Fix

Cache the results of [`GetComplexProperties`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryInitializationHandler.cs) per type using a static [`ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2).

### Implementation Options

- **Option A**: Static [`ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2) cache
  - Cache results indefinitely (type metadata doesn't change at runtime)
  - Thread-safe with `ConcurrentDictionary.GetOrAdd`

- **Option B**: [`ConditionalWeakTable<Type, IReadOnlyList<PropertyInfo>>`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2) for GC-friendly caching
  - Allows cached entries to be collected if the type is unloaded
  - Slightly more complex but more memory-safe in dynamic assembly scenarios

### Acceptance Criteria

- [ ] [`GetComplexProperties`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryInitializationHandler.cs) results are cached per type
- [ ] Cache is thread-safe
- [ ] Performance benchmark shows reduction in query initialization time
- [ ] No regression in behavior for dynamic types

### References

- [ConcurrentDictionary.GetOrAdd](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.getoradd)

---

## MED-5: FragmentEmittingVisitorBase.EmitFragment — Swallows Exceptions

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`FragmentEmittingVisitorBase.cs:23-34`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FragmentEmittingVisitorBase.cs:23-34) |
| **Status** | ✅ Resolved — exceptions logged at Error level and rethrown as InvalidOperationException |

### Description

In [`FragmentEmittingVisitorBase`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FragmentEmittingVisitorBase.cs), the [`EmitFragment`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FragmentEmittingVisitorBase.cs) method catches exceptions from fragment processing and silently downgrades them to `LogDebug`:

```csharp
try
{
    // ... fragment emission
}
catch (Exception ex)
{
    _logger.LogDebug(ex, "Failed to emit fragment");
    // Continues silently — no rethrow, no error return
}
```

This means a failed fragment emission produces no user-visible error. The resulting query is malformed, but the failure is invisible until query execution fails with a confusing error.

### Impact

- **Silent failures**: Fragment emission errors are hidden from developers.
- **Confusing query errors**: Instead of a clear "fragment X failed to emit" error, the user sees a generic database error.
- **Debugging difficulty**: The only trace of the failure is a `Debug`-level log message that may not be captured.

### Recommended Fix

Log as `LogError` and rethrow, or at minimum `LogWarning` with an error indicator in the fragment output.

### Implementation Options

- **Option A**: Rethrow with descriptive message
  - Log at `LogLevel.Error`
  - Rethrow as [`InvalidOperationException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception) with context about which fragment failed
  - This surfaces the error immediately to the caller

- **Option B**: Log as `Error` and return failure indicator
  - Change from `LogDebug` to `LogError`
  - Return a failure indicator that the renderer can detect
  - This allows higher-level error handling while still surfacing the issue

### Acceptance Criteria

- [ ] Fragment emission failures are logged at `Error` or `Warning` level
- [ ] Failures are surfaced to the caller (via exception or error return)
- [ ] The original exception details are preserved in the log

### References

- [Exception Handling Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)

---

## MED-6: Test Doubles Are Overly Complex; Tests in Single Monolithic File

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`FragmentRendererTests.cs`](../../../tests/Graph.Model.Age.Tests/FragmentRendererTests.cs) (2286 lines) |
| **Status** | ✅ Resolved — test doubles extracted to FragmentRendererTestDoubles.cs; main file reduced by ~160 lines |

### Description

[`FragmentRendererTests.cs`](../../../tests/Graph.Model.Age.Tests/FragmentRendererTests.cs) is a single 2286-line test file containing:

- 6 custom test double types totaling ~130 lines
- Reflection-heavy test setup code
- All fragment renderer tests in one monolithic class

This makes the test file:
- Difficult to navigate and understand
- Slow to compile and run (single test class can't be parallelized)
- Hard to maintain — changes to one area require understanding the entire file

### Impact

- **Test maintainability**: Large test files discourage adding new tests.
- **Slow test feedback**: Single-file tests can't be parallelized effectively.
- **Fragile tests**: Reflection-heavy setup is brittle to code changes.

### Recommended Fix

Split into feature-area test files. Replace reflection-based test doubles with a mocking framework.

### Implementation Options

- **Option A**: Split into 4+ files by feature, use [NSubstitute](https://nsubstitute.github.io/)
  - Split into: `FragmentRendererSimpleTests.cs`, `FragmentRendererWithClauseTests.cs`, `FragmentRendererOrderByTests.cs`, `FragmentRendererEdgeCaseTests.cs`
  - Replace manual test doubles with NSubstitute mocks
  - This reduces boilerplate and improves readability

- **Option B**: Keep hand-rolled fakes but reduce reflection
  - Consolidate the 6 test double types into fewer, simpler fakes
  - Remove reflection-heavy setup by using explicit test data
  - Keep as a single file but with clear region separators

### Acceptance Criteria

- [ ] Test file is split into multiple feature-area files (or clearly organized)
- [ ] Test doubles use a mocking framework or are significantly simplified
- [ ] All existing tests continue to pass
- [ ] New tests can be added to focused files without understanding the entire test suite

### References

- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

---

## MED-7: Inconsistent Null Checking Patterns

| Field | Value |
|-------|-------|
| **Severity** | 🟡 Medium |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | Various fragment visitors (e.g., [`FilteringFragmentVisitor`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FilteringFragmentVisitor.cs) vs [`AggregationFragmentVisitor`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/AggregationFragmentVisitor.cs)) |
| **Status** | ✅ Resolved — standardized on fail-fast with InvalidOperationException for critical null handlers |

### Description

Different fragment visitors handle null inputs differently:

- Some visitors (e.g., [`FilteringFragmentVisitor`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FilteringFragmentVisitor.cs)) silently return the source expression when a handler is null
- Other visitors (e.g., [`AggregationFragmentVisitor`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/AggregationFragmentVisitor.cs)) throw [`ArgumentNullException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) or [`NullReferenceException`](https://learn.microsoft.com/en-us/dotnet/api/system.nullreferenceexception) for null handlers

This inconsistency makes the codebase unpredictable — developers modifying one visitor cannot rely on patterns from another.

### Impact

- **Unpredictable behavior**: Similar operations behave differently across visitors.
- **Hard-to-find bugs**: Silent null handling can mask configuration errors.
- **Developer confusion**: Inconsistent patterns reduce codebase comprehensibility.

### Recommended Fix

Standardize on one null-handling pattern across all fragment visitors.

### Implementation Options

- **Option A**: Always throw with clear message
  - Standardize on throwing [`ArgumentNullException`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) with a descriptive parameter name
  - This ensures failures are loud and immediately visible

- **Option B**: Always log warning and return source expression (graceful degradation)
  - Standardize on logging a warning and returning the source expression
  - This allows partial rendering to proceed, with logged diagnostics
  - Useful in production scenarios where partial results are acceptable

### Acceptance Criteria

- [ ] All fragment visitors use the same null-handling pattern
- [ ] The chosen pattern is documented in the codebase style guide
- [ ] Tests verify null handling behavior for each visitor

### References

- [The Principle of Least Astonishment](https://en.wikipedia.org/wiki/Principle_of_least_astonishment)

---

## LOW-1: Minor Code Style Issues

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | Multiple files across the codebase |
| **Status** | ✅ Resolved — dotnet format applied; trailing whitespace and style issues fixed |

### Description

Multiple minor code style inconsistencies exist across the codebase:

- **Trailing blank lines**: Some files have inconsistent trailing newlines
- **Inconsistent `sealed`**: Some classes that could be `sealed` are not; some that shouldn't be, are
- **Mixed brace placement**: Some files use "Allman" style braces (`{` on new line), others use "K&R" (`{` on same line)
- **Missing XML documentation**: Many public methods and types lack proper XML doc comments
- **Inconsistent `this.` qualification**: Some parts use `this.` to qualify member access, others don't

While individually minor, these inconsistencies compound to create a less professional codebase.

### Impact

- **Code review friction**: Reviewers spend time discussing style instead of logic.
- **Onboarding friction**: New contributors must learn undocumented conventions.
- **Tooling integration**: Inconsistent style reduces effectiveness of automated formatting tools.

### Recommended Fix

Apply [`editorconfig`](https://editorconfig.org/) enforcement to standardize formatting. Run `dotnet format` as a CI step.

### Implementation Options

- **Option A**: Add [`.editorconfig`](https://editorconfig.org/) with formatting rules
  - Define rules for: indentation, brace placement, `sealed` usage, trailing whitespace, XML doc requirements
  - Configure `dotnet format` to enforce these rules
  - Run `dotnet format` once to normalize the codebase

- **Option B**: Run `dotnet format` as CI step
  - Add a CI step that runs `dotnet format --verify-no-changes`
  - Fail the build if formatting rules are violated
  - This is the enforcement mechanism for Option A

### Acceptance Criteria

- [ ] An `.editorconfig` file exists with project-wide formatting rules
- [ ] `dotnet format` passes without changes on the entire codebase
- [ ] CI includes a formatting verification step

### References

- [EditorConfig](https://editorconfig.org/)
- [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

---

## LOW-2: Parameter Name Mismatch in Format Strings

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeCypherEngine.cs:147-148`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:147-148) |
| **Status** | ✅ Resolved — string interpolation replaced with structured logging template |

### Description

Some logging statements use string interpolation (`$""`) instead of structured logging templates (`{NamedPlaceholder}`):

```csharp
// Uses string interpolation instead of structured logging
_logger.LogDebug($"Executing query: {cypher}");
```

Structured logging (e.g., `_logger.LogDebug("Executing query: {Cypher}", cypher)`) is preferred because:
- It preserves the parameter name in the logging system, enabling searching and filtering
- It avoids unnecessary string formatting when the log level is disabled
- It integrates with structured logging systems (Seq, ELK, Application Insights)

### Impact

- **Lost semantic data**: String interpolation strips the parameter name from the log event.
- **Performance overhead**: String formatting happens even when the log level is not enabled (though `Debug` logging has a guard, the string is still formatted).
- **Inconsistency**: Some logs use structured templates, others use interpolation.

### Recommended Fix

Convert all `$""` logger calls to template-based structured logging.

### Implementation Options

- **Option A**: Replace all `$""` logger calls with template-based logging
  - Change `_logger.LogDebug($"{msg}")` to `_logger.LogDebug("{Message}", msg)`
  - Audit all logging calls across the codebase

- **Option B**: Add a [Roslyn analyzer](https://learn.microsoft.com/en-us/dotnet/roslyn/analyzers/) to catch this pattern
  - Add a custom analyzer or configure an existing one to flag string interpolation in logger calls
  - This prevents regressions

### Acceptance Criteria

- [ ] All logging statements use structured templates (no `$""` in logger calls)
- [ ] Log parameter names are meaningful and consistent
- [ ] Existing tests verify log output where applicable

### References

- [Structured Logging in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging#structured-logging)

---

## LOW-3: IsNodeAlias Heuristic Is Fragile

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeFragmentRenderer.cs:698-699`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:698-699) |
| **Status** | ✅ Resolved — added XML docs documenting heuristic limitations and suggesting explicit alias tracking |

### Description

[`IsNodeAlias`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs) uses a fragile heuristic to determine whether an alias refers to a node or a relationship:

```csharp
// Heuristic: any alias starting with 'r' + digit is a relationship alias
private static bool IsNodeAlias(string alias)
    => !(alias.Length > 1 && alias[0] == 'r' && char.IsDigit(alias[1]));
```

This assumption breaks if:
- A node alias starts with `r` followed by a digit (e.g., `r1`, `r2`)
- A relationship alias doesn't follow the `r` + digit pattern
- Custom alias naming conventions are used

### Impact

- **Incorrect Cypher generation**: Misidentified aliases produce wrong Cypher syntax.
- **Hard-to-debug errors**: The heuristic fails silently, producing valid-looking but semantically wrong Cypher.
- **Fragility**: The heuristic depends on internal naming conventions that may change.

### Recommended Fix

Track alias roles explicitly in fragment metadata rather than relying on naming conventions.

### Implementation Options

- **Option A**: Add [`AliasRole`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum) enum (`Node`/`Relationship`) to fragments
  - Add an `AliasRole` property to the fragment metadata
  - Set the role when the alias is first assigned (in [`CypherQueryScope`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryScope.cs) or during fragment creation)
  - Use the explicit role instead of the heuristic

- **Option B**: Maintain [`IReadOnlySet<string>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlyset-1) of known relationship aliases
  - Track all assigned alias roles in a set
  - Pass this set to the renderer
  - Fall back to the heuristic only if the alias is not found in the set

### Acceptance Criteria

- [ ] Alias role detection no longer relies on naming conventions
- [ ] Explicit metadata tracks whether an alias is a node or relationship
- [ ] Tests verify correct rendering for aliases that would mislead the heuristic

### References

- [Avoid Heuristic-Based Type Detection](https://en.wikipedia.org/wiki/Heuristic_(computer_science))

---

## LOW-4: RegexOptions.Compiled in Static Initializers

| Field | Value |
|-------|-------|
| **Severity** | 🔵 Low |
| **Category** | Code Quality |
| **Source Review** | Code Quality Review |
| **Affected File(s)** | [`AgeFragmentRenderer.cs:665-667,701-703`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:665-667) |
| **Status** | ✅ Resolved — static Regex fields changed to Lazy<Regex> to defer compilation |

### Description

Static [`Regex`](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex) fields are initialized with [`RegexOptions.Compiled`](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions) in static initializers:

```csharp
private static readonly Regex NodeAliasPattern = new(@"^r\d+", RegexOptions.Compiled);
private static readonly Regex RelationshipAliasPattern = new(@"^\(.*\)$", RegexOptions.Compiled);
```

[`RegexOptions.Compiled`](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions) causes compilation of the regex to IL at the time the type is initialized, which adds startup cost. If these regexes are used infrequently, the compilation cost may outweigh the runtime performance benefit.

### Impact

- **Increased startup time**: Regex compilation at type initialization adds to application startup latency.
- **Memory overhead**: Compiled regexes use more memory than interpreted ones.
- **Cold start penalty**: In serverless or short-lived process scenarios, the compilation cost is paid but never amortized.

### Recommended Fix

Consider using [`Lazy<Regex>`](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1) or [`RegexOptions.NonBacktracking`](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions) (.NET 7+) for patterns that don't require backtracking.

### Implementation Options

- **Option A**: Use [`Lazy<Regex>`](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1) for each pattern
  - Defer compilation to first use rather than type initialization
  - Reduces startup cost while keeping compiled regex performance

- **Option B**: Use [`RegexOptions.NonBacktracking`](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions) (.NET 7+)
  - If the patterns don't require backtracking, use `NonBacktracking` which has lower startup cost
  - Remove `Compiled` for simple patterns where interpretation is fast enough

### Acceptance Criteria

- [ ] Regex compilation is deferred or replaced with a more efficient option
- [ ] Startup time is measured and improved
- [ ] No regression in regex matching behavior

### References

- [RegexOptions.Compiled Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-regex#compiled-vs-interpreted)
- [RegexOptions.NonBacktracking (.NET 7+)](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions)

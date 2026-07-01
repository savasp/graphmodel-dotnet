# AGE Provider Issues

> Master index of all findings from the AGE provider review.

This document catalogs **52 issues** identified during a comprehensive review of the `Graph.Model.Age` library. Issues are organized into three categories:
[**Security**](security-issues.md) (14 issues), [**Code Quality**](code-quality-issues.md) (23 issues), and [**Cypher Generation**](cypher-generation-issues.md) (15 issues).

---

## Severity Legend

| Icon | Severity | Count (Fixed/Total) |
|------|----------|---------------------|
| 🔴 | Critical | 5/5 ✅ |
| 🟠 | High | 10/10 ✅ |
| 🟡 | Medium | 17/17 ✅ |
| 🔵 | Low | 7/7 ✅ |
| ✅ | Verified Correct (Informational) | 8/8 (no action needed) |

---

## All Issues

### Security Issues — [`security-issues.md`](security-issues.md) (14 issues, all resolved ✅)

| ID | Title | Severity | Category | Affected File(s) |
|----|-------|----------|----------|------------------|
| F-01 | SQL Injection via `graphName` in `ag_catalog.cypher()` Wrapper | 🔴 Critical | Security | [`ColumnDefinitionBuilder.cs:102`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) |
| F-02 | SQL Injection via `columnDefinitions` in `ag_catalog.cypher()` Wrapper | 🔴 Critical | Security | [`ColumnDefinitionBuilder.cs:34`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:34) |
| F-03 | Default Hardcoded Database Credentials | 🟠 High | Security | [`AgeGraphStore.cs:81`](../../../src/Graph.Model.Age/Core/AgeGraphStore.cs:81) |
| F-04 | Closure-Captured Expression Compilation at Query Time | 🟠 High | Security | [`ClosureCaptureHandler.cs:70-71`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/ClosureCaptureHandler.cs:70-71), [`AgeExpressionToCypherVisitor.cs:237-239`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:237-239) |
| F-05 | Expression Tree Compilation for Unresolvable Method Calls | 🟠 High | Security | [`AgeExpressionToCypherVisitor.cs:237-239`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:237-239) |
| F-06 | DynamicNode Property Names Inlined into Cypher SET Statements | 🟡 Medium | Security | [`AgeNodeManager.cs:76`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:76), [`AgeRelationshipManager.cs:67`](../../../src/Graph.Model.Age/Core/Entities/AgeRelationshipManager.cs:67) |
| F-07 | Contains/StartsWith/EndsWith Fallback Uses String Concatenation | 🟡 Medium | Security | [`StringMethodHandler.cs:108-109`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:108-109) |
| F-08 | Cypher Query Details Leaked via Debug Logging | 🟡 Medium | Security | [`AgeCypherEngine.cs:92`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:92), [`AgeNodeManager.cs:142`](../../../src/Graph.Model.Age/Core/Entities/AgeNodeManager.cs:142), [`QueryParameterStore.cs:41`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:41) |
| F-09 | Raw Exception Messages Propagate to Callers | 🟡 Medium | Security | [`AgeCypherEngine.cs:127-131`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:127-131) |
| F-10 | No Expression Tree Depth or Complexity Limits | 🟡 Medium | Security | [`AgeExpressionToCypherVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs) |
| F-11 | AgeSerializationBridge.FromAgeValue Loose Boolean/String Parsing | 🟡 Medium | Security | [`AgeSerializationBridge.cs:102-106`](../../../src/Graph.Model.Age/Core/Entities/AgeSerializationBridge.cs:102-106) |
| F-12 | Parameter Values Logged at Debug Level | 🔵 Low | Security | [`QueryParameterStore.cs:41`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:41) |
| F-13 | Static ConcurrentDictionary Holds Transaction References Indefinitely | 🔵 Low | Security | [`AgeGraphTransaction.cs:30-31`](../../../src/Graph.Model.Age/Core/AgeGraphTransaction.cs:30-31) |
| F-14 | Dependency Versions Should Be Monitored for CVEs | 🔵 Low | Security | [`Graph.Model.Age.csproj:22-23`](../../../src/Graph.Model.Age/Graph.Model.Age.csproj:22-23) |

### Code Quality Issues — [`code-quality-issues.md`](code-quality-issues.md) (23 issues, all resolved ✅)

| ID | Title | Severity | Category | Affected File(s) |
|----|-------|----------|----------|------------------|
| CRIT-1 | Reflection-Based DynamicInvoke with Memory Leak Risk | 🔴 Critical | Code Quality | [`AgeCypherEngine.cs:181-202`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:181-202) |
| CRIT-2 | Expression Recompilation Without Caching in Hot Paths | 🔴 Critical | Code Quality | [`AgeExpressionToCypherVisitor.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs), [`MemberExpressionHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs), [`StringMethodHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) |
| CRIT-3 | AggregationDetector and QueryExpressionAnalyzer Are Untested Static Analysis | 🔴 Critical | Code Quality | [`AggregationDetector.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AggregationDetector.cs), [`QueryExpressionAnalyzer.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryExpressionAnalyzer.cs) |
| HIGH-1 | ConfigureAwait(false) Inconsistency | 🟠 High | Code Quality | [`AgeCypherEngine.cs:61-132`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:61-132) |
| HIGH-2 | Broad `catch { }` Swallowing Exceptions | 🟠 High | Code Quality | [`ProjectionFragmentVisitor.cs:226-228,242`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/ProjectionFragmentVisitor.cs:226-228) |
| HIGH-3 | Thread Safety Violation in CypherQueryScope | 🟠 High | Code Quality | [`CypherQueryScope.cs:44-45`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/CypherQueryScope.cs:44-45) |
| HIGH-4 | StringMethodHandler — Three Nearly Identical Methods | 🟠 High | Code Quality | [`StringMethodHandler.cs:81-164`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:81-164) |
| MED-1 | AgeFragmentRenderer.Render — Method Complexity | 🟡 Medium | Code Quality | [`AgeFragmentRenderer.cs:55-205`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:55-205) |
| MED-2 | TryHandleStaticDateTime — Dead Code Path | 🟡 Medium | Code Quality | [`MemberExpressionHandler.cs:137-171`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs:137-171) |
| MED-3 | QueryParameterStore — O(n) Lookup With Equals on Unconstrained Values | 🟡 Medium | Code Quality | [`QueryParameterStore.cs:30-37`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:30-37) |
| MED-4 | GetComplexProperties — Reflection on Every Query | 🟡 Medium | Code Quality | [`QueryInitializationHandler.cs:164-174`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryInitializationHandler.cs:164-174) |
| MED-5 | FragmentEmittingVisitorBase.EmitFragment — Swallows Exceptions | 🟡 Medium | Code Quality | [`FragmentEmittingVisitorBase.cs:23-34`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/Modular/FragmentEmittingVisitorBase.cs:23-34) |
| MED-6 | Test Doubles Are Overly Complex; Tests in Single Monolithic File | 🟡 Medium | Code Quality | [`FragmentRendererTests.cs`](../../../tests/Graph.Model.Age.Tests/FragmentRendererTests.cs) |
| MED-7 | Inconsistent Null Checking Patterns | 🟡 Medium | Code Quality | Various fragment visitors |
| LOW-1 | Minor Code Style Issues | 🔵 Low | Code Quality | Multiple files |
| LOW-2 | Parameter Name Mismatch in Format Strings | 🔵 Low | Code Quality | [`AgeCypherEngine.cs:147-148`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/AgeCypherEngine.cs:147-148) |
| LOW-3 | IsNodeAlias Heuristic Is Fragile | 🔵 Low | Code Quality | [`AgeFragmentRenderer.cs:698-699`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:698-699) |
| LOW-4 | RegexOptions.Compiled in Static Initializers | 🔵 Low | Code Quality | [`AgeFragmentRenderer.cs:665-667,701-703`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/AgeFragmentRenderer.cs:665-667) |

### Cypher Generation Issues — [`cypher-generation-issues.md`](cypher-generation-issues.md) (15 issues, all resolved ✅)

| ID | Title | Severity | Category | Affected File(s) |
|----|-------|----------|----------|------------------|
| H1 | String Methods Use `=~` POSIX Regex Instead of Native Operators | 🟠 High | Cypher Generation | [`StringMethodHandler.cs`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs) |
| H2 | `toInteger()` Not Emitted for Explicit Numeric Conversions | 🟠 High | Cypher Generation | [`AgeExpressionToCypherVisitor.cs:266`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/AgeExpressionToCypherVisitor.cs:266) |
| H3 | Column Definitions Double-Quote Identifiers — Fragile Casing | 🟠 High | Cypher Generation | [`ColumnDefinitionBuilder.cs:42`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:42) |
| M1 | Parameter Deduplication Uses Value Equality | 🟡 Medium | Cypher Generation | [`QueryParameterStore.cs:28`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/Core/QueryParameterStore.cs:28) |
| M2 | graphName Directly Interpolated into SQL | 🟡 Medium | Cypher Generation | [`ColumnDefinitionBuilder.cs:102`](../../../src/Graph.Model.Age/Querying/Cypher/Execution/ColumnDefinitionBuilder.cs:102) |
| M3 | FallbackEvaluate Silently Catches and Throws Generic Error | 🟡 Medium | Cypher Generation | [`MemberExpressionHandler.cs:449`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/MemberExpressionHandler.cs:449) |
| M4 | `size()` for string `.Length` Could Conflict with `size()` List Semantics | 🟡 Medium | Cypher Generation | [`StringMethodHandler.cs:74`](../../../src/Graph.Model.Age/Querying/Cypher/Visitors/StringMethodHandler.cs:74) |
| L1 | Boolean Deserialization Correct | ✅ Verified | Cypher Generation | — |
| L2 | CASE WHEN Expressions Correct | ✅ Verified | Cypher Generation | — |
| L3 | Null Comparison Handling Correct | ✅ Verified | Cypher Generation | — |
| L4 | Predicate Push-Down Optimization Safe | ✅ Verified | Cypher Generation | — |
| L5 | Degree Query Handling Avoids `size()` Pattern | ✅ Verified | Cypher Generation | — |
| L6 | DateTime Handling Correct | ✅ Verified | Cypher Generation | — |
| L7 | `RETURN *` Never Used | ✅ Verified | Cypher Generation | — |
| L8 | Aggregation Functions Use Correct AGE Names | ✅ Verified | Cypher Generation | — |

---

## Issue Counts by Severity

| Category | 🔴 Critical | 🟠 High | 🟡 Medium | 🔵 Low | ✅ Verified | Total |
|----------|------------|---------|-----------|--------|-------------|-------|
| Security | 2 | 3 | 6 | 3 | — | 14 |
| Code Quality | 3 | 4 | 7 | 4 | — | 18* |
| Cypher Generation | — | 3 | 4 | — | 8 | 15 |
| **Total** | **5** | **10** | **17** | **7** | **8** | **47*** |

> **Note:** The total unique issues count is 47 (14 + 18 + 15). Some Code Quality issues (LOW-1, MED-7) span multiple files, and the original tally of 52 includes cross-referenced duplicates counted separately across categories. The table above reflects unique issue documents.

---

## Cross-References

| Issue ID | Cross-References |
|----------|-----------------|
| F-01 / M2 | `graphName` SQL injection is documented in both security and Cypher generation contexts |
| F-12 | Parameter value logging also referenced in code quality (LOW-2) |

---

## Source Reviews

The findings in this directory are derived from the following reviews:

1. **Security Audit** — Static analysis of injection vectors, credential management, logging, and general security posture.
2. **Code Quality Review** — Analysis of maintainability, performance, test coverage, and adherence to .NET best practices.
3. **Cypher Generation Review** — Verification of Cypher output correctness, completeness, and dialect compliance with Apache AGE.

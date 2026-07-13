# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | `Cvoya.Graph.CompatibilityTests` 1.0.0-alpha.20251014.0 |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-13 / local strict certifying run; CI uses the same lane |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | No | AGE full-text semantics are not exposed by this provider. |
| Transactions | Yes | One PostgreSQL connection and transaction per graph transaction. |
| NestedTransactions | No | |
| ComplexPropertyCascade | Yes | Owned value nodes are deleted transactionally. |
| CallSubqueries | No | |
| PatternSizeProjection | No | |
| MultiLabelMatch | Yes | Logical inheritance labels are lowered to AGE-compatible predicates. |
| OrderByEntity | Yes | Entity ordering is lowered to a stable AGE-compatible key. |
| ShortestPath | No | |
| OptionalTraversal | Yes | Optional matches are lowered while preserving owners with absent paths. |

## Results

| Runtime cases | Passed | Skipped (declared capability) | Statically skipped | Failed |
|---|---|---|---|---|
| 390 | 356 | 33 | 1 | 0 |

The compatibility inventory contains 370 runnable test methods. For this capability set,
`ComplianceInventory.MinimumExecuted(declared)` is 337 methods and the strict compliance guard
passes. Theory data rows make the runtime case count slightly larger than the method inventory.
The suite also contains one statically skipped, issue-tracked test; the inventory deliberately
excludes it, so it is not counted as a capability skip above. The provider-specific adapter,
dialect, SQL-envelope, and security tests are also excluded from the table.

Reproduce:

```bash
scripts/containers/start-age.sh
export AGE_CONNECTION_STRING='Host=localhost;Port=5455;Username=postgres;Password=postgres;Database=postgres'
GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test tests/Graph.Age.Tests/Graph.Age.Tests.csproj --configuration Debug
```

Every runtime skip counted in the capability column carries the suite's declared-capability
reason. Any failed case, unexpected dynamic skip, unavailable-store skip under strict mode, or
compliance-guard failure invalidates this report.

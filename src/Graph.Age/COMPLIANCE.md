# Cvoya.Graph.Age compatibility compliance report

| | |
|---|---|
| Provider | `Cvoya.Graph.Age` (issue #86 implementation) |
| Compliance suite | `Cvoya.Graph.CompatibilityTests` 1.0.0-alpha.20251014.0 |
| Backing store | Apache AGE 1.7.0 / PostgreSQL 18 |
| Date / run | 2026-07-11 / local strict certifying run; CI uses the same lane |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | No | AGE full-text semantics are not exposed by this provider. |
| Transactions | Yes | One PostgreSQL connection and transaction per graph transaction. |
| NestedTransactions | No | |
| ComplexPropertyCascade | Yes | Owned value nodes are deleted transactionally. |
| CallSubqueries | No | |
| PatternSizeProjection | No | |
| MultiLabelMatch | No | Logical inheritance labels are lowered by the provider; native multi-label matching is not declared. |
| OrderByEntity | No | Provider lowering supports the shared queries without claiming native support. |
| ShortestPath | No | |
| OptionalTraversal | No | Provider lowering supports required shared queries without claiming native support. |

## Results

| Executed cases | Passed | Skipped (declared capability) | Failed |
|---|---|---|---|
| 361 | 337 | 24 | 0 |

The compatibility inventory contains 348 runnable test methods. For this capability set,
`ComplianceInventory.MinimumExecuted(declared)` is 324 methods and the strict compliance guard
passes. Theory data rows make the runtime case count slightly larger than the method inventory.
The suite also contains nine statically skipped, issue-tracked tests; the inventory deliberately
excludes them, so they are not counted as capability skips above. The provider-specific adapter,
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

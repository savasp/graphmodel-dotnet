# CVOYA graph compatibility compliance report

A provider is **compliant** when its compatibility run reports N passed / M skipped-with-a-declared-capability-reason / 0 failed, where N is at least `ComplianceInventory.MinimumExecuted(declared)` for the capabilities the provider declares. Copy this template into your provider's repository/package and fill it in per release.

| | |
|---|---|
| Provider | \<Cvoya.Graph.YourProvider\> \<provider version\> |
| Compliance suite | Cvoya.Graph.CompatibilityTests \<suite version\> |
| Backing store | \<e.g. "Apache AGE 1.7 / PostgreSQL 16"\> |
| Date / CI run | \<date\> / \<link\> |

## Declared capabilities

| Capability | Declared | Note |
|---|---|---|
| FullTextSearch | Yes / No | |
| Transactions | Yes / No | |
| NestedTransactions | Yes / No | |
| ComplexPropertyCascade | Yes / No | |
| CallSubqueries | Yes / No | |
| PatternSizeProjection | Yes / No | |
| MultiLabelMatch | Yes / No | |
| OrderByEntity | Yes / No | |
| ShortestPath | Yes / No | |
| OptionalTraversal | Yes / No | |

## Results

| Executed | Passed | Skipped (capability) | Failed |
|---|---|---|---|
| N | N | M | 0 |

Minimum required executed for this capability set: `ComplianceInventory.MinimumExecuted(declared)` = \<value\>.

Reproduce:

```bash
GRAPHMODEL_COMPLIANCE_STRICT=1 dotnet test <your-test-project> --report-trx
```

A failed run (any `Failed` count above 0, or the `ComplianceGuard` assembly fixture throwing) means the provider is not compliant with this suite version. A skip is only compliant when it carries a capability skip reason of the form `Capability '<Name>' not declared by provider '<ProviderName>' (Cvoya.Graph.CompatibilityTests <version>)` for a capability the table above marks "No" - any other skip or failure needs investigation.

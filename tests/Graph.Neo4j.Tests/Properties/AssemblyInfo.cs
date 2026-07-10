
// Parallel test execution is controlled by the Neo4jTestCollection definition

// Arms the GraphModel compatibility suite's executed-count guard for this assembly. Only
// enforces anything when GRAPHMODEL_COMPLIANCE_STRICT=1 is set (CI compliance lanes); a local run
// with no reachable Neo4j stays a plain skip. See Cvoya.Graph.CompatibilityTests.ComplianceGuard.
[assembly: AssemblyFixture(typeof(Cvoya.Graph.CompatibilityTests.ComplianceGuard))]

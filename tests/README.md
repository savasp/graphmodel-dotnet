# Tests

The `src/Graph.CompatibilityTests` project (packed as `Cvoya.Graph.CompatibilityTests`) contains [xUnit](https://xunit.net/) tests, defined as interfaces with default-implemented methods, which any provider can bind to via the harness SPI (`IGraphProviderTestHarness`, `CompatibilityTest`). You can't run these tests directly - see [docs/provider-implementers-guide.md](../docs/provider-implementers-guide.md#certifying-a-provider) for the full workflow.

Provider projects under `tests/` bind the suite to the in-memory, Neo4j, and Apache AGE implementations. The compatibility meta-tests and the other fast projects need no backing service.

## Running the Test Lanes

The repository runner discovers test projects, excludes benchmarks, and verifies that every selected project reports a nonzero test count:

```bash
./scripts/run-tests.sh --fast
./scripts/run-tests.sh --lane neo4j
./scripts/run-tests.sh --lane age
./scripts/run-tests.sh --lane all
```

The provider lanes require the corresponding service configuration described below. Agent-run snapshot tests should add `--disable-diff-engine`.

## Running Neo4j Tests

There are two supported ways to run the Neo4j tests:

### 1. Against a Neo4j Instance

You will have to provide via environmental variables the details of the Neo4j instance, whether local or remote. Please note that databases with names such as `test<GUID>` are going to appear. The test infrastructure attempts to clean up after itself by deleting these tests. However, some may be left behind in case of crashes. Don't use a production database instance to run these tests.

Environment variables:

- `NEO4J_URI` - Connection URI (default: bolt://localhost:7687)
- `NEO4J_USER` - Username (default: neo4j)
- `NEO4J_PASSWORD` - Password (default: password)
- `NEO4J_DATABASE` - Database name (default: neo4j)

Example:

```bash
NEO4J_URI=bolt://localhost:7687 ./scripts/run-tests.sh --lane neo4j
```

If a Neo4j instance is reachable at `bolt://localhost:7687` with `neo4j/password`, the test fixture uses it by default:

```bash
./scripts/run-tests.sh --lane neo4j
```

### 2. Against a Local Container

Start a local Neo4j container before running the full integration suite:

```bash
./scripts/containers/start-neo4j.sh
./scripts/run-tests.sh --lane neo4j
```

The script tries Podman first and Docker second. Set `CONTAINER_RUNTIME=podman` or `CONTAINER_RUNTIME=docker` to force a runtime.

The repository test runner also wires the local container credentials for you:

```bash
./scripts/run-tests.sh --lane neo4j --neo4j -c Debug
```

## Running Apache AGE Tests

Supply `AGE_CONNECTION_STRING`, or let the runner start the repository container:

```bash
./scripts/run-tests.sh --lane age --age
```

### CI/CD Configuration

GitHub Actions provides Neo4j and AGE as job services for provider validation. Setting `CI=true` locally does not start either service.

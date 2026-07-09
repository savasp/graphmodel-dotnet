# Tests

The `src/Graph.Model.CompatibilityTests` project (packed as `Cvoya.Graph.Model.CompatibilityTests`) contains [xUnit](https://xunit.net/) tests, defined as interfaces with default-implemented methods, which any provider can bind to via the harness SPI (`IGraphProviderTestHarness`, `CompatibilityTest`). You can't run these tests directly - see [docs/provider-implementers-guide.md](../docs/provider-implementers-guide.md#certifying-a-provider) for the full workflow.

The `Graph.Model.Neo4j.Tests` project implements the harness SPI and binds the suite's interfaces, using `Graph.Model.Neo4j` which implements the Graph Model abstraction layer. `Graph.Model.CompatibilityTests.Tests` holds meta-tests for the suite itself (harness lifecycle, capability skips, the compliance guard) and needs no backing store.

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
NEO4J_URI=bolt://localhost:7687 dotnet test tests/Graph.Model.Neo4j.Tests/Graph.Model.Neo4j.Tests.csproj
```

If a Neo4j instance is reachable at `bolt://localhost:7687` with `neo4j/password`, the test fixture uses it by default:

```bash
dotnet test tests/Graph.Model.Neo4j.Tests/Graph.Model.Neo4j.Tests.csproj
```

### 2. Against a Local Container

Start a local Neo4j container before running the full integration suite:

```bash
./scripts/containers/start-neo4j.sh
dotnet test tests/Graph.Model.Neo4j.Tests/Graph.Model.Neo4j.Tests.csproj
```

The script tries Podman first and Docker second. Set `CONTAINER_RUNTIME=podman` or `CONTAINER_RUNTIME=docker` to force a runtime.

The repository test runner also wires the local container credentials for you:

```bash
./scripts/run-tests.sh --neo4j -c Debug
```

### CI/CD Configuration

GitHub Actions provides Neo4j as a job service for the provider test job. Setting `CI=true` locally does not start Neo4j.

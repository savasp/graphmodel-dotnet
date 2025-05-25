# Tests

The `Graph.Model.Tests` project contains [xUnit](https://xunit.net/) tests which can be used with any implementation of `Graph.Model.IGraph`. You can't run these tests directly.

The `Graph.Provider.Neo4j.Tests` project runs these tests using the `Graph.Provider.Neo4j.Neo4jGraphProvider` implementation of `Graph.Model.IGraph`. The test project also contains Neo4j-specific tests.

## Running Neo4j Tests

There are two ways to run the Neo4j tests:

### 1. Against a Neo4j Instance

You will have to provide via environmental variables the details of the Neo4j instance, whether local or remote. Please note that databases with names such as `test<GUID>` are going to appear. Even though these test databases are automatically deleted from the Neo4j instance, some may be left behind in case of a crash. Don't use a production database instance to run these tests.

Environment variables:

- `NEO4J_URI` - Connection URI (default: bolt://localhost:7687)
- `NEO4J_USER` - Username (default: neo4j)
- `NEO4J_PASSWORD` - Password (default: password)
- `NEO4J_DATABASE` - Database name (default: neo4j)
- `USE_NEO4J_CONTAINERS` - Set to `false` to use the Neo4j instance

Example:

```bash
USE_NEO4J_CONTAINERS=false dotnet test Graph.Provider.Neo4j.Tests
```

### 2. Against Docker Containers

The tests can be performed against docker-hosted containers running Neo4j. The containers are automatically downloaded and get started. The Docker daemon needs to be running on the machine that hosts the tests.

Environment variables:

- `USE_NEO4J_CONTAINERS` - Set to `true` to use Docker containers (default in CI)

Example:

```bash
USE_NEO4J_CONTAINERS=true dotnet test Graph.Provider.Neo4j.Tests
```

### CI/CD Configuration

In CI environments (like GitHub Actions), the tests automatically use Docker containers. The CI environment variable is automatically set to `true` by most CI systems, which triggers container-based testing.

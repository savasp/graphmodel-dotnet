# Tests

The `Graph.Model.Tests` project contains [xUnit](https://xunit.net/) tests, contained in abstract classes, which can be used with any implementation of `Graph.Model.IGraph`. You can't run these tests directly.

The `Graph.Model.Neo4j.Tests` project runs implements the abstract classes and uses `Graph.Model.Neo4j` which implements the Graph Model abstraction layer.

## Running Neo4j Tests

There are two ways to run the Neo4j tests:

### 1. Against a Neo4j Instance

You will have to provide via environmental variables the details of the Neo4j instance, whether local or remote. Please note that databases with names such as `test<GUID>` are going to appear. The test infrastructure attempts to clean up after itself by deleting these tests. However, some may be left behind in case of crashes. Don't use a production database instance to run these tests.

Environment variables:

- `NEO4J_URI` - Connection URI (default: bolt://localhost:7687)
- `NEO4J_USER` - Username (default: neo4j)
- `NEO4J_PASSWORD` - Password (default: password)
- `NEO4J_DATABASE` - Database name (default: neo4j)
- `USE_NEO4J_CONTAINERS` - Set to `false` to use the Neo4j instance

Example:

```bash
USE_NEO4J_CONTAINERS=false dotnet test Graph.Model.Neo4j.Tests
```

or, if you don't have the `USE_NEO4J_CONTAINERS` or `CI` environment variables set to `true`, simply

```bash
dotnet test Graph.Model.Neo4j.Tests
```

### 2. Against Docker Containers

The tests can be performed against docker-hosted containers running Neo4j. The containers are automatically downloaded and get started. The Docker daemon needs to be running on the machine that hosts the tests.

Environment variables:

- `USE_NEO4J_CONTAINERS` - Set to `true` to use Docker containers (default in CI)
- Or, simply set `CI` to `true` to use the continuous integration configuration.

Example:

```bash
USE_NEO4J_CONTAINERS=true dotnet test Graph.Model.Neo4j.Tests
```

### CI/CD Configuration

In CI environments (like GitHub Actions), the tests automatically use Docker containers as long as the CI environment variable is set to `true`.

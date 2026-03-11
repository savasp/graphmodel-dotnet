---
name: build-and-test
description: Build the solution and run all unit tests. Use after implementing features, fixing bugs, or before committing.
user-invocable: true
allowed-tools: Bash(dotnet *)
argument-hint: "[configuration]"
---

# Build and Test

Build and test the GraphModel solution.

Configuration defaults to `Debug`. Pass a configuration name as argument to override (e.g., `/build-and-test Release`).

## Steps

1. Build the solution:
   ```bash
   dotnet build --configuration ${0:-Debug}
   ```

2. Run unit tests:
   ```bash
   dotnet test tests/Graph.Model.Tests/ --configuration ${0:-Debug} --no-build
   ```

3. Report results — if any tests fail, show the failing test names and error messages with file paths.

## Notes

- For integration tests (Neo4j), use `CI=true dotnet test tests/Graph.Model.Neo4j.Tests/` separately — these require Docker.
- For package testing, use `LocalFeed` then `Release` configuration. See [docs/graph-model-developers.md](../../docs/graph-model-developers.md).

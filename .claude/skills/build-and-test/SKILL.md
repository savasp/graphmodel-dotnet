---
name: build-and-test
description: Build the solution and run the test suite. Use after implementing features, fixing bugs, or before committing.
user-invocable: true
allowed-tools: Bash(dotnet *)
argument-hint: "[configuration]"
---

# Build and Test

Build and test the GraphModel solution. Configuration defaults to `Debug`; pass one as `$1` to override (e.g. `/build-and-test Release`).

## Steps

1. Build the solution (use `Debug` if `$1` is empty):
   ```bash
   dotnet build --configuration $1
   ```

2. Run the analyzer tests — these always run, no external dependencies:
   ```bash
   dotnet test tests/Graph.Model.Analyzers.Tests --configuration $1 --no-build
   ```

3. Run the full suite, which includes the Neo4j-bound contract tests:
   ```bash
   dotnet test --configuration $1 --no-build
   ```
   This requires a running Neo4j (`NEO4J_URI`, default `bolt://localhost:7687`). Start one with `scripts/containers/start-neo4j.sh` if Docker is available. **There is no automatic container startup.** If no Neo4j is reachable, skip this step and say so explicitly in your report — an analyzers-only pass is not full validation.

4. Report results — failing test names and error messages with file paths, plus which steps ran.

## Notes

- `tests/Graph.Model.Tests` is an abstract contract suite — running it directly executes ~no tests. The contract tests execute through `tests/Graph.Model.Neo4j.Tests`.
- For package testing, use `LocalFeed` then `Release` configuration. See [docs/graph-model-developers.md](../../../docs/graph-model-developers.md).

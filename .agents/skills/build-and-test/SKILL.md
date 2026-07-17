---
name: build-and-test
description: Build the solution and run the test suite. Use after implementing features, fixing bugs, or before committing.
user-invocable: true
argument-hint: "[configuration]"
---

# Build and Test

Build and test the CVOYA graph solution. Configuration defaults to `Debug`; pass one as `$1` to override (for example, `/build-and-test Release`). Agent-run tests must disable DiffEngine so Verify failures stay in terminal output.

## Steps

1. Build the explicit solution. This assignment implements the promised `Debug` default:

   ```bash
   configuration="${1:-Debug}"
   dotnet build cvoya-graph.sln --configuration "$configuration"
   ```

2. Run the fast provider-free/in-memory lane. Every command names a real test project and must report a nonzero test count; an exit code of zero with no discovered tests is not a successful lane:

   ```bash
   configuration="${1:-Debug}"
   export DiffEngine_Disabled=true
   for project in \
       tests/Graph.Analyzers.Tests/Graph.Analyzers.Tests.csproj \
       tests/Graph.CompatibilityTests.Tests/Graph.CompatibilityTests.Tests.csproj \
       tests/Graph.Core.Tests/Graph.Core.Tests.csproj \
       tests/Graph.Cypher.Tests/Graph.Cypher.Tests.csproj \
       tests/Graph.InMemory.Tests/Graph.InMemory.Tests.csproj \
       tests/Graph.Neo4j.Translation.Tests/Graph.Neo4j.Translation.Tests.csproj \
       tests/Graph.QuerySurface.CompilationTests/Graph.QuerySurface.CompilationTests.csproj \
       tests/Graph.Serialization.CodeGen.Tests/Graph.Serialization.CodeGen.Tests.csproj; do
       dotnet test "$project" --configuration "$configuration" --no-build
   done
   ```

3. Run the Neo4j integration lane when Neo4j is reachable. Use `scripts/containers/start-neo4j.sh` to start the default `bolt://localhost:7687` instance with `neo4j/password`; set `CONTAINER_RUNTIME=podman` or `CONTAINER_RUNTIME=docker` to force a runtime, or provide `NEO4J_URI`, `NEO4J_USER`, and `NEO4J_PASSWORD`:

   ```bash
   configuration="${1:-Debug}"
   DiffEngine_Disabled=true dotnet test tests/Graph.Neo4j.Tests/Graph.Neo4j.Tests.csproj --configuration "$configuration" --no-build
   ```

4. Run the Apache AGE integration lane when AGE is reachable. Use `scripts/containers/start-age.sh` and export the `AGE_CONNECTION_STRING` it prints (the default uses port `5455`), or supply an existing connection string:

   ```bash
   configuration="${1:-Debug}"
   DiffEngine_Disabled=true dotnet test tests/Graph.Age.Tests/Graph.Age.Tests.csproj --configuration "$configuration" --no-build
   ```

5. When both databases are available, the explicit full-suite command is:

   ```bash
   configuration="${1:-Debug}"
   DiffEngine_Disabled=true dotnet test cvoya-graph.sln --configuration "$configuration" --no-build
   ```

6. Report failing test names and error messages with file paths, the observed test counts, and which database-backed lanes ran. If a configured service remains unavailable after trying its container script, report that lane as skipped; a fast-lane pass is not full validation.

## Notes

- `src/Graph.CompatibilityTests/Graph.CompatibilityTests.csproj` is the packable provider contract suite. It executes essentially no tests standalone; the contracts run through `tests/Graph.InMemory.Tests`, `tests/Graph.Neo4j.Tests`, and `tests/Graph.Age.Tests`.
- `tests/Graph.Performance.Tests` contains benchmarks and is not part of the normal test gate.
- For package-reference validation, run `dotnet msbuild eng/PackageValidation.proj -target:Validate`. See [the build-system guide](../../../docs/graph-model-developers.md).

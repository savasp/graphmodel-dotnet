---
name: build-and-test
description: Build the solution and run the test suite. Use after implementing features, fixing bugs, or before committing.
user-invocable: true
argument-hint: "[configuration]"
---

# Build and Test

Build and test the CVOYA graph solution. Configuration defaults to `Debug`; pass one as `$1` to override (for example, `/build-and-test Release`). Use the repository test runner so new test projects are picked up without duplicating an inventory in this skill.

## Steps

1. Run the fast lane. It builds the solution, discovers all test projects under `tests/`, excludes benchmarks and external-service projects, and includes the in-memory provider contract suite:

   ```bash
   configuration="${1:-Debug}"
   ./scripts/run-tests.sh --configuration "$configuration" --lane fast --disable-diff-engine
   ```

2. Run each relevant database-backed lane after starting its repository container or configuring an existing service. Reuse the build from step 1:

   ```bash
   configuration="${1:-Debug}"
   ./scripts/run-tests.sh --configuration "$configuration" --lane neo4j --no-build --disable-diff-engine
   ./scripts/run-tests.sh --configuration "$configuration" --lane age --no-build --disable-diff-engine
   ```

   `scripts/containers/start-neo4j.sh` prepares the default Neo4j endpoint. `scripts/containers/start-age.sh` prints the `AGE_CONNECTION_STRING` to export. The runner can start either service itself with `--neo4j` or `--age`.

3. When both services are available, `--lane all` is the single full-suite command. Run package-reference validation separately when build or packaging behavior changes:

   ```bash
   configuration="${1:-Debug}"
   ./scripts/run-tests.sh --configuration "$configuration" --lane all --disable-diff-engine
   dotnet msbuild eng/PackageValidation.proj -target:Validate
   ```

4. Report the project and test counts printed by the runner, failing test names and errors, and any database-backed lane that could not run. A fast-lane pass is not full provider validation.

The provider/service matrix and compatibility-suite semantics are canonical in [AGENTS.md](../../../AGENTS.md).

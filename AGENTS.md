# CVOYA graph — Project instructions

Type-safe .NET library for graph data and graph databases, with LINQ querying, transactions, and optional Roslyn analyzers and serialization codegen. Neo4j and PostgreSQL + Apache AGE are the in-tree database providers. Apache 2.0 licensed. Targets **.NET 10** with **C# 14** (`LangVersion` is set in [Directory.Build.props](Directory.Build.props)).

This file is the canonical instruction set for AI coding agents (Claude Code, Codex, Copilot, and others) and a good orientation for humans. Tool-specific configuration lives in `.claude/`, `.codex/`, and `.github/copilot-instructions.md`; see [docs/ai-agents.md](docs/ai-agents.md) for the map.

## Layout

```
src/Graph/                          provider-neutral core: IGraph, INode, IRelationship, LINQ surface, attributes
src/Graph.Neo4j/                    Neo4j provider: LINQ-to-Cypher, transactions, entity managers
src/Graph.Age/                      PostgreSQL + Apache AGE provider
src/Graph.InMemory/                 in-memory reference provider: LINQ-to-objects over the shared query model; test double
src/Graph.Cypher/                   shared typed Cypher AST, validation, and rendering
src/Graph.Analyzers/                Roslyn analyzers (CG001…) for consumer domain models
src/Graph.Serialization/            runtime serialization representation (EntityInfo, schemas)
src/Graph.Serialization.CodeGen/    incremental source generator for entity serializers
src/Graph.CompatibilityTests/       packable provider contract suite (TCK): harness SPI, capability registry, guard
tests/                              see "Build and test" — the projects differ in what they need
examples/                           runnable usage examples
docs/                               concept docs, developer/build docs
scripts/                            release + container helper scripts
```

## Build and test

```bash
dotnet build --configuration Debug
./scripts/run-tests.sh --configuration Debug --lane fast --disable-diff-engine
./scripts/run-tests.sh --configuration Debug --lane all --disable-diff-engine
```

The runner discovers test projects under `tests/`, separates service-free and provider-backed lanes, and rejects projects that report zero tests. The full lane needs both Neo4j and AGE; use configured services, the repository container scripts, or the runner's `--neo4j --age` options.

The test projects have different requirements — get this right:

| Project | What it is | Needs |
|---------|------------|-------|
| `src/Graph.CompatibilityTests` | **Provider contract suite (TCK), packed as `Cvoya.Graph.CompatibilityTests`.** Test interfaces with default xUnit methods, a harness SPI (`IGraphProviderTestHarness`), and a capability registry; providers bind those interfaces in their own test project. It executes ~no tests standalone. Add provider-agnostic tests here so every provider inherits them. See [docs/provider-implementers-guide.md](docs/provider-implementers-guide.md#certifying-a-provider). | nothing (but running it alone proves nothing) |
| `tests/Graph.Neo4j.Tests` | The contract suite bound to Neo4j + provider-specific tests. This is where the suite actually runs. | a running Neo4j at `NEO4J_URI`, or reachable at the default `bolt://localhost:7687` with `neo4j/password`. Start one with `scripts/containers/start-neo4j.sh` (Podman preferred locally; Docker fallback; set `CONTAINER_RUNTIME=podman` or `CONTAINER_RUNTIME=docker` to force one). There is **no** automatic container startup — `CI=true` does nothing (that path is disabled; see #88). |
| `tests/Graph.Age.Tests` | The contract suite bound to Apache AGE + provider-specific tests. | a running AGE instance at `AGE_CONNECTION_STRING`. Start one with `scripts/containers/start-age.sh` and export the connection string it prints (default host port `5455`). |
| `tests/Graph.InMemory.Tests` | The contract suite bound to the in-memory provider. Full-text search tests skip via the capability declaration. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.CompatibilityTests.Tests` | Meta-tests for the TCK itself (harness SPI lifecycle, capability skips, the compliance guard). | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Analyzers.Tests` | Analyzer tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Core.Tests` | Provider-neutral graph model, query-shape, and serialization integration tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Cypher.Tests` | Shared Cypher AST, validation, and rendering tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Neo4j.Translation.Tests` | LINQ-to-Cypher translation tests that do not execute against Neo4j. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.QuerySurface.CompilationTests` | Compile-time query-surface contract tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Serialization.CodeGen.Tests` | Incremental serialization generator tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Performance.Tests` | Benchmarks. | not part of the normal gate |

Package testing before publishing: `dotnet msbuild eng/PackageValidation.proj -target:Validate`. The orchestrator packs the complete LocalFeed set, verifies its inventory and assembly version metadata with `scripts/verify-package-set.sh`, and restores/builds package references using repository-scoped NuGet state. Untagged builds use `VERSION` as their development default; published releases are tag-authoritative and override it. See [docs/release-process.md](docs/release-process.md).

## Conventions

- C#/.NET conventions per [CONTRIBUTING.md](CONTRIBUTING.md); match the style of surrounding code, don't reformat.
- One public type per file; XML documentation on all new public APIs.
- Apache 2.0 copyright header on new source files, matching `.editorconfig`: `// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.` followed by `// See LICENSE in the project root for full license terms.`
- Conventional commit messages: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
- Async: public async APIs take a `CancellationToken` and have an `Async` suffix.
- Analyzer diagnostics use the `CG###` series. Check `src/Graph.Analyzers/AnalyzerReleases.*.md` before allocating an unused ID; suppress a diagnostic via `.editorconfig` or `#pragma warning disable CG###`.
- Keep changes minimal and focused; prefer editing existing files; file follow-up issues instead of expanding scope.

## Multi-agent workflow

- **The lead session owns isolation.** Task agents are dispatched into an already-prepared worktree/branch — verify with `git status` / `git branch --show-current` before changing anything, and never work in the user's main checkout. (Worktrees live under `~/dev/worktrees/graph/<task>`, based on latest `origin/main`.)
- One focused branch + PR per task (`feat/…`, `fix/…`, `chore/…`); coordinate through branch state and PR comments, not shared files.
- Run the relevant test lane before pushing (fast lane at minimum; include the Neo4j/AGE lanes when provider behavior changes). Docs-only changes may skip the test lane. The Release build + `dotnet format` gate is enforced mechanically instead of by instruction: `eng/install-hooks.sh`, run once per clone, points `core.hooksPath` at `.githooks/`, and every linked worktree inherits it — do not re-run it per worktree, and reserve `git push --no-verify` for emergencies.
- Local CodeQL is not required: hosted CI runs CodeQL on every pull request and merge-queue candidate. `./scripts/run-codeql.sh` (or `./scripts/validate-build.sh --codeql`) remains available as an opt-in check for security-sensitive changes — default portable mode; `--build-mode manual` depends on local compiler-tracing support. It snapshots the tree when it starts, so run it on the final diff.
- Hosted CI validates pull requests and merge-queue candidates, not the resulting `main` push. Its required path restores and builds once, then runs the provider and library suites against shared Neo4j and Apache AGE services; keep new tests in that consolidated path rather than adding another build job.
- **Shared-file discipline:** `cvoya-graph.sln`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `VERSION`, and `.github/` workflows are high-conflict and/or protected — change them additively, and ask the user before modifying the protected ones (a PreToolUse hook enforces this for Claude and Codex; it is advisory, not a security boundary).
- **All changes land via pull request** (branch protection enforces this); use standard `git`/`gh`. You may see commits and PRs authored by `savasp-agent[bot]` — that is the maintainer's own automation identity, not a tool contributors need or can use.

## Issue tracking

- **Native relationships over prose:** dependencies use GitHub's sub-issue / blocked-by links, not "blocked by #N" in text. Umbrella issues (e.g. #90) group work via sub-issues.
- **Issue types** carry category: `Bug`, `Feature`, or `Task`. Use labels only for triage and area attributes such as `documentation`, `ci`, `security`, `release`, `code-quality`, `testing`, `architecture`, and `agents`; milestones are used only for release-bounded groups.
- PRs reference their issue; repeat the closing keyword per issue: `Closes #64, closes #65` (comma-separated bare numbers silently don't close).
- Follow-ups become issues, not TODO comments or scope creep.

## Documentation discipline

- Ship doc updates with the code that changes behavior (`docs/`, XML docs, README).
- Code samples in docs must compile against the current API — if you change a public API, grep the docs for it.

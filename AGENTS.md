# CVOYA graph — Project instructions

Type-safe .NET library for graph data and graph databases, with LINQ querying, transactions, and optional Roslyn analyzers and serialization codegen. Neo4j is the in-tree provider; a PostgreSQL + Apache AGE provider is planned (#53, #90). Apache 2.0 licensed. Targets **.NET 10** with **C# 14** (`LangVersion` is set in [Directory.Build.props](Directory.Build.props)).

This file is the canonical instruction set for AI coding agents (Claude Code, Codex, Copilot, and others) and a good orientation for humans. Tool-specific configuration lives in `.claude/`, `.codex/`, and `.github/copilot-instructions.md`; see [docs/ai-agents.md](docs/ai-agents.md) for the map.

## Layout

```
src/Graph/                          provider-neutral core: IGraph, INode, IRelationship, LINQ surface, attributes
src/Graph.Neo4j/                    Neo4j provider: LINQ-to-Cypher, transactions, entity managers
src/Graph.InMemory/                 in-memory reference provider: LINQ-to-objects over the shared query model; test double
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
dotnet build --configuration Debug     # day-to-day build (project references)
dotnet test  --configuration Debug     # full suite — needs a local Neo4j (see below)
```

The test projects have different requirements — get this right:

| Project | What it is | Needs |
|---------|------------|-------|
| `src/Graph.CompatibilityTests` | **Provider contract suite (TCK), packed as `Cvoya.Graph.CompatibilityTests`.** Test interfaces with default xUnit methods, a harness SPI (`IGraphProviderTestHarness`), and a capability registry; providers bind those interfaces in their own test project. It executes ~no tests standalone. Add provider-agnostic tests here so every provider inherits them. See [docs/provider-implementers-guide.md](docs/provider-implementers-guide.md#certifying-a-provider). | nothing (but running it alone proves nothing) |
| `tests/Graph.Neo4j.Tests` | The contract suite bound to Neo4j + provider-specific tests. This is where the suite actually runs. | a running Neo4j at `NEO4J_URI`, or reachable at the default `bolt://localhost:7687` with `neo4j/password`. Start one with `scripts/containers/start-neo4j.sh` (Podman preferred locally; Docker fallback; set `CONTAINER_RUNTIME=podman` or `CONTAINER_RUNTIME=docker` to force one). There is **no** automatic container startup — `CI=true` does nothing (that path is disabled; see #88). |
| `tests/Graph.InMemory.Tests` | The contract suite bound to the in-memory provider. Full-text search tests skip via the capability declaration. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.CompatibilityTests.Tests` | Meta-tests for the TCK itself (harness SPI lifecycle, capability skips, the compliance guard). | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Analyzers.Tests` | Analyzer tests. | nothing — runs anywhere; the fast no-Docker lane |
| `tests/Graph.Performance.Tests` | Benchmarks. | not part of the normal gate |

Package testing before publishing: `dotnet build --configuration LocalFeed`, then `--configuration Release`. Release builds require the `VERSION` file; the release process (tag-triggered, `VERSION` as the source of truth) is described in [docs/release-process.md](docs/release-process.md).

## Conventions

- C#/.NET conventions per [CONTRIBUTING.md](CONTRIBUTING.md); match the style of surrounding code, don't reformat.
- One public type per file; XML documentation on all new public APIs.
- Apache 2.0 copyright header on new source files: `// Copyright CVOYA. Licensed under the Apache License, Version 2.0.`
- Conventional commit messages: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
- Async: public async APIs take a `CancellationToken` and have an `Async` suffix.
- Analyzer diagnostics use `CG0XX` prefix (CVOYA Graph codes CG001–CG009). Suppress via `.editorconfig` or `#pragma warning disable CG0XX`.
- Keep changes minimal and focused; prefer editing existing files; file follow-up issues instead of expanding scope.

## Multi-agent workflow

- **The lead session owns isolation.** Task agents are dispatched into an already-prepared worktree/branch — verify with `git status` / `git branch --show-current` before changing anything, and never work in the user's main checkout. (Worktrees live under `~/dev/worktrees/graph/<task>`, based on latest `origin/main`.)
- One focused branch + PR per task (`feat/…`, `fix/…`, `chore/…`); coordinate through branch state and PR comments, not shared files.
- Build and test before pushing. For branches with code changes, also run `./scripts/run-codeql.sh` before pushing commits to a remote branch; `./scripts/validate-build.sh --codeql` satisfies this when running the full validation pass. Docs-only changes may skip the test and CodeQL gates. The default CodeQL mode is the required portable local gate. `./scripts/run-codeql.sh --build-mode manual` is optional and depends on CodeQL compiler tracing support for the local platform/toolchain; if manual mode fails after the default command succeeds, record it as a manual tracing limitation with the CodeQL version and relevant log path, not as a failed CodeQL gate.
- **Shared-file discipline:** `cvoya-graph.sln`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `VERSION`, and `.github/` workflows are high-conflict and/or protected — change them additively, and ask the user before modifying the protected ones (a PreToolUse hook enforces this for Claude; it is advisory, not a security boundary).
- **All changes land via pull request** (branch protection enforces this); use standard `git`/`gh`. You may see commits and PRs authored by `savasp-agent[bot]` — that is the maintainer's own automation identity, not a tool contributors need or can use.

## Issue tracking

- **Native relationships over prose:** dependencies use GitHub's sub-issue / blocked-by links, not "blocked by #N" in text. Umbrella issues (e.g. #90) group work via sub-issues.
- **Issue types** carry category: `Bug`, `Feature`, or `Task`. Use labels only for triage and area attributes such as `documentation`, `ci`, `security`, `release`, `code-quality`, `testing`, `architecture`, and `agents`; milestones are used only for release-bounded groups.
- PRs reference their issue; repeat the closing keyword per issue: `Closes #64, closes #65` (comma-separated bare numbers silently don't close).
- Follow-ups become issues, not TODO comments or scope creep.

## Documentation discipline

- Ship doc updates with the code that changes behavior (`docs/`, XML docs, README).
- Code samples in docs must compile against the current API — if you change a public API, grep the docs for it.

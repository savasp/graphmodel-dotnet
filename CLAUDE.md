# GraphModel — Project context for Claude Code

Type-safe .NET library for graph data and graph databases (Neo4j), with LINQ, transactions, and optional analyzers/codegen. Targets **.NET 10**.

## Build and test

```bash
# Development (project references)
dotnet build --configuration Debug
dotnet test --configuration Debug
```

**Package testing (before publishing):** `dotnet build --configuration LocalFeed` then `dotnet build --configuration Release`. Release builds require a `VERSION` file and the `CreateRelease` MSBuild target. See [docs/graph-model-developers.md](docs/graph-model-developers.md).

## Stack

- .NET 10, C# 12
- NuGet (no npm/pnpm)

## Key locations

| Area | Path |
|------|------|
| Core | `src/Graph.Model/` |
| Neo4j provider | `src/Graph.Model.Neo4j/` |
| Analyzers | `src/Graph.Model.Analyzers/` |
| Serialization | `src/Graph.Model.Serialization` and `src/Graph.Model.Serialization.CodeGen` |
| Examples | `examples/` |
| Unit tests | `tests/Graph.Model.Tests/` |
| Neo4j integration tests | `tests/Graph.Model.Neo4j.Tests/` |

## Multi-agent setup

This project is configured for multiple Claude Code agents working in parallel. Each agent **must** use a worktree (`EnterWorktree`) for isolation.

| Agent | File | Role |
|-------|------|------|
| **engineer** | `.claude/agents/engineer.md` | Implements features, fixes bugs, refactors code |
| **qa-engineer** | `.claude/agents/qa-engineer.md` | Writes tests, validates changes, checks coverage |
| **reviewer** | `.claude/agents/reviewer.md` | Reviews code for correctness, style, architecture |

### Worktree rules

- **Always** start with `EnterWorktree` — never modify the main working directory directly.
- Create a feature branch before making changes.
- Each agent works independently; coordinate via branches and PRs.
- Build and test in your worktree before pushing.

### Orchestration pipeline

Typical task flow (lead session coordinates):

1. **engineer** implements the feature/fix on a branch, pushes a draft PR.
2. **qa-engineer** + **reviewer** run in parallel against the engineer's branch.
3. If issues are found, **engineer** is dispatched again to fix.
4. When qa-engineer and reviewer both pass, the PR is ready for merge.

### Hooks

- **`PreToolUse` (Edit/Write)**: Blocks edits to protected files (`VERSION`, `.github/`, `Directory.Build.props`, etc.). Ask the user before modifying these.
- **`PostToolUse` (Edit/Write)**: Auto-builds the affected project after `.cs` file edits to catch compile errors early.

### Skills

| Skill | Trigger | Description |
|-------|---------|-------------|
| `/build-and-test` | Manual or auto | Build solution and run unit tests |
| `/new-node-type` | Manual | Scaffold a new node type with tests |
| `/new-analyzer` | Manual | Scaffold a new Roslyn analyzer with tests |
| `/graphmodel` | Auto | Load project context for GraphModel work |

## More detail

- **Build and release:** [docs/graph-model-developers.md](docs/graph-model-developers.md)
- **Core concepts (nodes, relationships, LINQ):** [docs/core-concepts.md](docs/core-concepts.md)
- **Contributing:** [CONTRIBUTING.md](CONTRIBUTING.md)
- **AI agent docs:** [docs/ai-agents.md](docs/ai-agents.md)

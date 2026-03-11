# GraphModel

Type-safe .NET graph library for Neo4j with LINQ, transactions, and optional analyzers/codegen. Targets .NET 10.

For full project context, build, test, and conventions, see **[CLAUDE.md](CLAUDE.md)**.

## Agents

Agent definitions are in `.claude/agents/`:

| Agent | Description |
|-------|-------------|
| **engineer** | Implements features, fixes bugs, refactors code |
| **qa-engineer** | Writes tests, validates changes, checks coverage |
| **reviewer** | Reviews code for correctness, style, architecture |

All agents must work in isolated worktrees. See [CLAUDE.md](CLAUDE.md) for worktree rules.

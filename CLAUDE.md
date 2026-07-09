# GraphModel — Claude Code entry point

**Read [AGENTS.md](AGENTS.md) first — it is the canonical project instruction set** (layout, build/test requirements, conventions, multi-agent workflow, issue tracking). This file only adds what is Claude Code-specific.

## Claude-specific configuration

| What | Where |
|------|-------|
| Task agents (engineer, qa-engineer, reviewer) | `.claude/agents/` — the lead session prepares a worktree/branch and dispatches agents into it |
| Skills | `.claude/skills/` — `/build-and-test [config]`, `/new-node-type <Name>`, `/new-analyzer <Id> <Title>`, `/graphmodel` (context) |
| Hooks | `.claude/hooks/` — `protect-files.sh` (PreToolUse: blocks Edit/Write on `VERSION`, `.github/`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.claude/`, `.codex/` — ask the user first; advisory, not a security boundary) and `verify-build.sh` (PostToolUse: builds the affected project after `.cs` edits and feeds compile errors back) |
| Permissions | `.claude/settings.json` pre-approves `dotnet`/`git`/`gh` and common read-only commands |

## Quick reference

```bash
dotnet build --configuration Debug   # build
dotnet test  --configuration Debug   # full suite — Neo4j required; see AGENTS.md "Build and test"
dotnet test tests/Graph.Model.Analyzers.Tests --configuration Debug   # fast lane, no Docker
```

Test-project semantics (contract suite vs. integration vs. analyzers) are in AGENTS.md — do not assume `src/Graph.Model.CompatibilityTests` runs the unit tests; it is an abstract provider contract suite (packed as `Cvoya.Graph.Model.CompatibilityTests`).

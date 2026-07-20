# CVOYA graph — Claude Code entry point

**Read [AGENTS.md](AGENTS.md) first — it is the canonical project instruction set** (layout, build/test requirements, conventions, multi-agent workflow, issue tracking). This file only adds what is Claude Code-specific.

## Claude-specific configuration

| What | Where |
|------|-------|
| Task agents (engineer, qa-engineer, reviewer) | `.claude/agents/` — the lead session prepares a worktree/branch and dispatches agents into it |
| Skills | `.claude/skills/` — `/build-and-test [config]`, `/new-node-type <Name>`, `/new-analyzer <Id> <Title>`, `/cvoya-graph` (context) |
| Hooks | `.claude/hooks/` — `protect-files.sh` (PreToolUse: blocks Edit/Write on `VERSION`, `.github/`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.claude/`, `.codex/` — ask the user first; advisory, not a security boundary) and `verify-build.sh` (PostToolUse: builds the affected project after `.cs` edits and feeds compile errors back) |
| Permissions | `.claude/settings.json` pre-approves `dotnet`/`git`/`gh` and common read-only commands |
| Plugins | `.claude/settings.json` `enabledPlugins` keeps `csharp-lsp` and turns off the web/mobile LSPs (`typescript`, `swift`, `kotlin`, `pyright`), `playwright`, and `frontend-design` — this repo is a headless .NET library |
| Search scope | `.claudeignore` excludes build output (`bin/`, `obj/`, `artifacts/`, `TestResults/`) and `local-nuget-feed/` |

## Quick reference

```bash
./scripts/run-tests.sh --configuration Debug --lane fast --disable-diff-engine
./scripts/run-tests.sh --configuration Debug --lane all --neo4j --age --disable-diff-engine
dotnet msbuild eng/PackageValidation.proj -target:Validate   # package behavior only
```

The repository runner discovers test projects under `tests/`; service requirements and compatibility-suite semantics are canonical in AGENTS.md.

## Diagnostic codes

Compile-time analyzer codes use the `CG###` series. Consult `src/Graph.Analyzers/AnalyzerReleases.*.md` before allocating the next unused ID. Suppress a diagnostic via `.editorconfig` or `#pragma warning disable CG###`.

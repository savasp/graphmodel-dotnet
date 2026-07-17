---
---

# AI agent documentation

This repository is set up so AI coding agents can discover project context, build and test commands, and tool-specific configuration.

**The single source of truth is [AGENTS.md](https://github.com/cvoya-com/graph/blob/main/AGENTS.md)** at the repo root: layout, build/test and provider-service requirements, conventions, multi-agent workflow, and issue-tracking rules. Tool-specific files layer on top of it:

| Tool | What to use |
|------|-------------|
| **Claude Code** | [CLAUDE.md](https://github.com/cvoya-com/graph/blob/main/CLAUDE.md) (thin entry point → AGENTS.md). Task agents in `.claude/agents/`, skills in `.claude/skills/`, hooks in `.claude/hooks/`, settings in `.claude/settings.json`. |
| **Codex** | Reads [AGENTS.md](https://github.com/cvoya-com/graph/blob/main/AGENTS.md) natively. Agent role definitions in `.codex/agents/*.toml`, configuration in `.codex/config.toml`, hooks in `.codex/hooks.json`. |
| **GitHub Copilot** | [.github/copilot-instructions.md](https://github.com/cvoya-com/graph/blob/main/.github/copilot-instructions.md) (thin pointer → AGENTS.md). |
| **Other tools** (Zed, ChatGPT, etc.) | [AGENTS.md](https://github.com/cvoya-com/graph/blob/main/AGENTS.md) directly. |

## Multi-agent setup

Three specialized roles are defined for both Claude Code (`.claude/agents/*.md`) and Codex (`.codex/agents/*.toml`):

| Agent | Role |
|-------|------|
| **engineer** | Implements features, fixes bugs, refactors code |
| **qa-engineer** | Writes tests, validates changes, checks coverage |
| **reviewer** | Reviews code for correctness, style, architecture |

### Isolation model

The **lead session** owns isolation: it creates a worktree and branch per task and dispatches agents into that prepared workspace. Agents verify their workspace (`git status`, `git branch --show-current`) before making changes and never touch the user's main checkout. Coordination happens via branches and pull requests, not shared files. See AGENTS.md § "Multi-agent workflow".

### Guardrails

- **Protected files** (`VERSION`, `.github/`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.claude/`, `.codex/`): a PreToolUse hook blocks direct edits and tells the agent to ask the user first. This is advisory, not a security boundary. Self-test: `bash .claude/hooks/hooks.test.sh`.
- **Build feedback**: a PostToolUse hook builds the affected project after `.cs` edits and feeds compile errors back to the agent.
- **Permissions**: `.claude/settings.json` pre-approves the repository test runner and common development commands (`dotnet`, `git`, `gh`, etc.) so agents can work without manual approval prompts.

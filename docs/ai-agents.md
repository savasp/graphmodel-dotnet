# AI agent documentation

This repository is set up so AI coding agents can discover project context, build and test commands, and tool-specific skills or rules.

| Tool / standard | What to use |
|-----------------|-------------|
| **Claude Code** | [CLAUDE.md](../CLAUDE.md) at repo root (project memory). Agent definitions in `.claude/agents/`. Project skills in `.claude/skills/`. Project settings in `.claude/settings.json`. |
| **Cursor** | [AGENTS.md](../AGENTS.md) points to CLAUDE.md. Rules in `.cursor/rules/`. |
| **Other tools (Zed, ChatGPT, etc.)** | [AGENTS.md](../AGENTS.md) at repo root; open [CLAUDE.md](../CLAUDE.md) for full build, test, and project context. |

Single source of truth for project context is **CLAUDE.md**.

## Multi-agent setup

Three specialized agents are defined in `.claude/agents/`:

| Agent | File | Role |
|-------|------|------|
| **engineer** | `.claude/agents/engineer.md` | Implements features, fixes bugs, refactors code |
| **qa-engineer** | `.claude/agents/qa-engineer.md` | Writes tests, validates changes, checks coverage |
| **reviewer** | `.claude/agents/reviewer.md` | Reviews code for correctness, style, architecture |

### Worktree isolation

All agents **must** use worktrees (`EnterWorktree`) to avoid conflicts. Each agent gets an isolated copy of the repository and works on its own branch. Coordination happens via branches and pull requests.

### Project settings

`.claude/settings.json` pre-approves common development commands (`dotnet build`, `dotnet test`, `git`, `gh`, etc.) so agents can work without manual approval prompts.

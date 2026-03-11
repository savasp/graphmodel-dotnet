# AI agent documentation

This repository is set up so AI coding agents can discover project context, build and test commands, and tool-specific skills or rules.

| Tool / standard | What to use |
|-----------------|-------------|
| **Claude Code** | [CLAUDE.md](../CLAUDE.md) at repo root (project memory). Project skills in `.claude/skills/` (e.g. `.claude/skills/graphmodel/`). |
| **Cursor** | [AGENTS.md](../AGENTS.md) points to CLAUDE.md. Rules in `.cursor/rules/`. Optional project skill in `.cursor/skills/graphmodel/`. |
| **Other tools (Zed, ChatGPT, etc.)** | [AGENTS.md](../AGENTS.md) at repo root; open [CLAUDE.md](../CLAUDE.md) for full build, test, and project context. |

Single source of truth for project context is **CLAUDE.md**. It contains the one-line project description, build/test commands, stack (.NET 10, C# 12), and key folder locations, with links to detailed docs.

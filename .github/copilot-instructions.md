# Copilot instructions

Read **[AGENTS.md](../AGENTS.md)** at the repository root — it is the canonical instruction set for this project (layout, build and test requirements, conventions, workflow). Everything there applies to Copilot.

Quick anchors:

- The project layout and test/provider-service semantics: AGENTS.md § "Layout" and § "Build and test". The compatibility suite under `src/` executes through provider test projects; use `scripts/run-tests.sh` rather than maintaining a second project inventory.
- Coding conventions (one public type per file, XML docs on public APIs, Apache 2.0 header, async rules, conventional commits): AGENTS.md § "Conventions" and [CONTRIBUTING.md](../CONTRIBUTING.md).
- Never build Cypher by interpolating values into query strings — values go through parameters.

Style: answer like a friendly colleague; keep explanations simple and grounded in the existing codebase.

# Copilot instructions

Read **[AGENTS.md](../AGENTS.md)** at the repository root — it is the canonical instruction set for this project (layout, build and test requirements, conventions, workflow). Everything there applies to Copilot.

Quick anchors:

- The project layout and what each `src/` and `tests/` project is for: AGENTS.md § "Layout" and § "Build and test". Note that `tests/Graph.Model.Tests` is an abstract provider contract suite — it executes through `tests/Graph.Model.Neo4j.Tests`, which needs a running Neo4j.
- Coding conventions (one public type per file, XML docs on public APIs, Apache 2.0 header, async rules, conventional commits): AGENTS.md § "Conventions" and [CONTRIBUTING.md](../CONTRIBUTING.md).
- Never build Cypher by interpolating values into query strings — values go through parameters.

Style: answer like a friendly colleague; keep explanations simple and grounded in the existing codebase.

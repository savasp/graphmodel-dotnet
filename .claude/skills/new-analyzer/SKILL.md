---
name: new-analyzer
description: Scaffold a new Roslyn analyzer for GraphModel, including the analyzer, code fix, and tests.
user-invocable: true
argument-hint: "<DiagnosticId> <title>"
---

# Create a New Roslyn Analyzer

Scaffold a new Roslyn analyzer with code fix and tests.

## Arguments

- `$1` — Diagnostic ID (e.g., `GM0010`)
- `$2` — Short title describing what the analyzer checks

## Steps

1. **Read existing analyzers** in `src/Graph.Model.Analyzers/` to understand:
   - Naming conventions (e.g., `GM001`, `GM002`)
   - Diagnostic categories and severities used
   - How analyzers are registered
   - Code fix patterns

2. **Create the analyzer class** in `src/Graph.Model.Analyzers/`:
   - Follow the `DiagnosticAnalyzer` pattern from existing analyzers
   - Use the provided diagnostic ID and title
   - Add a meaningful message format and description
   - Register for the appropriate syntax/symbol actions

3. **Create a code fix provider** if applicable, registered for the analyzer's diagnostic ID.

4. **Add tests** in `tests/Graph.Model.Analyzers.Tests/` — one test class per diagnostic, following the existing `GM00X_*Tests.cs` pattern (uses `Microsoft.CodeAnalysis.Testing`).

5. **Build and test** (no Docker needed for analyzer work):
   ```bash
   dotnet build src/Graph.Model.Analyzers/ --configuration Debug
   dotnet test tests/Graph.Model.Analyzers.Tests --configuration Debug
   ```

## References

- Existing analyzers in `src/Graph.Model.Analyzers/` are the primary reference
- [Roslyn Analyzer docs](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)

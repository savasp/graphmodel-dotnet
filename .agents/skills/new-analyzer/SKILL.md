---
name: new-analyzer
description: Scaffold a new Roslyn analyzer for CVOYA graph, including the analyzer, code fix, and tests.
user-invocable: true
argument-hint: "<DiagnosticId> <title>"
---

# Create a New Roslyn Analyzer

Scaffold a new Roslyn analyzer with code fix and tests.

## Arguments

- `$1` — Diagnostic ID (e.g., `CG0010`)
- `$2` — Short title describing what the analyzer checks

## Steps

1. **Read existing analyzers** in `src/Graph.Analyzers/` to understand:
   - Naming conventions (e.g., `CG001`, `CG002`)
   - Diagnostic categories and severities used
   - How analyzers are registered
   - Code fix patterns

2. **Create the analyzer class** in `src/Graph.Analyzers/`:
   - Follow the `DiagnosticAnalyzer` pattern from existing analyzers
   - Use the provided diagnostic ID and title
   - Add a meaningful message format and description
   - Register for the appropriate syntax/symbol actions

3. **Create a code fix provider** if applicable, registered for the analyzer's diagnostic ID.

4. **Add tests** in `tests/Graph.Analyzers.Tests/` — one test class per diagnostic, following the existing `CG00X_*Tests.cs` pattern (uses `Microsoft.CodeAnalysis.Testing`).

5. **Build and test** (no Docker needed for analyzer work):
   ```bash
   dotnet build src/Graph.Analyzers/Graph.Analyzers.csproj --configuration Debug
   DiffEngine_Disabled=true dotnet test tests/Graph.Analyzers.Tests/Graph.Analyzers.Tests.csproj --configuration Debug
   ```

## References

- Existing analyzers in `src/Graph.Analyzers/` are the primary reference
- [Roslyn Analyzer docs](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)

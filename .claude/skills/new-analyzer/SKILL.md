---
name: new-analyzer
description: Add a Roslyn diagnostic to the existing CVOYA graph analyzer, including release metadata and tests.
user-invocable: true
argument-hint: "<DiagnosticId> <title>"
---

# Add a Roslyn Diagnostic

Add a rule to the repository's existing analyzer architecture. Do not create a separate analyzer or code-fix project unless the requested behavior requires a new shipping component.

## Arguments

- `$1` — unused diagnostic ID in the `CG###` series (for example, `CG016`)
- `$2` — short title describing what the diagnostic checks

## Steps

1. Read the existing descriptor, analyzer, resource, release-tracking, and test patterns under `src/Graph.Analyzers/` and `tests/Graph.Analyzers.Tests/`.
2. Add the descriptor and localized resource strings, update the checked-in `Resources.Designer.cs` accessors, then register the analysis in the shared `GraphAnalyzer` implementation.
3. Record the diagnostic in `AnalyzerReleases.Unshipped.md` and match the existing severity/category conventions.
4. Add focused positive, negative, inheritance/generic, diagnostic-location, and argument coverage as applicable. The repository currently ships diagnostics without code-fix providers, so do not invent one merely because a fix could be imagined.
5. Run the focused analyzer tests, then the repository fast lane with DiffEngine disabled for the agent run:

   ```bash
   DiffEngine_Disabled=true dotnet test --project tests/Graph.Analyzers.Tests/Graph.Analyzers.Tests.csproj --configuration Debug
   ./scripts/run-tests.sh --configuration Debug --lane fast --disable-diff-engine
   ```

Microsoft's [Roslyn analyzer documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix) is a secondary reference; the existing repository implementation is authoritative for local structure.

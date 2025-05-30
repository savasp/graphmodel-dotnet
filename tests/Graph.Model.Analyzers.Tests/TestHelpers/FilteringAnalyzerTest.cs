// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Cvoya.Graph.Model.Analyzers.Tests;

internal class FilteringAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    protected override async Task<(Compilation, ImmutableArray<Diagnostic>)> GetProjectCompilationAsync(
        Project project,
        IVerifier verifier,
        CancellationToken cancellationToken)
    {
        var (compilation, diagnostics) = await base.GetProjectCompilationAsync(project, verifier, cancellationToken);

        // Filter out CS1705 version conflict errors that occur with .NET 9
        var filteredDiagnostics = diagnostics
            .Where(d => d.Id != "CS1705")
            .ToImmutableArray();

        return (compilation, filteredDiagnostics);
    }
}
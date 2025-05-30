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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cvoya.Graph.Model.Analyzers.Rules.Validators;

/// <summary>
/// Validates that only classes (not structs) implement INode or IRelationship (GM001).
/// </summary>
internal class StructImplementationValidator : ITypeValidator
{
    public IEnumerable<Diagnostic> Validate(INamedTypeSymbol typeSymbol, SymbolAnalysisContext context)
    {
        // Only check value types (structs)
        if (!typeSymbol.IsValueType)
            yield break;

        // Check if it implements INode or IRelationship
        var implementedInterface = GetImplementedInterface(typeSymbol);
        if (string.IsNullOrEmpty(implementedInterface))
            yield break;

        // Report the diagnostic
        yield return Diagnostic.Create(
            DiagnosticDescriptors.OnlyClassesCanImplement,
            typeSymbol.Locations.FirstOrDefault(),
            typeSymbol.Name,
            implementedInterface);
    }

    private static string? GetImplementedInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var @interface in typeSymbol.AllInterfaces)
        {
            // For now, just check the name to debug
            if (@interface.Name == "INode")
            {
                return "INode";
            }

            if (@interface.Name == "IRelationship")
            {
                return "IRelationship";
            }
        }

        return null;
    }

    private static bool IsGraphModelInterface(INamedTypeSymbol @interface)
    {
        var namespaceName = @interface.ContainingNamespace?.ToDisplayString() ?? "";
        // Check for both possible namespace patterns
        return namespaceName == "Cvoya.Graph.Model" ||
               namespaceName == "Graph.Model" ||
               namespaceName.EndsWith(".Graph.Model");
    }
}
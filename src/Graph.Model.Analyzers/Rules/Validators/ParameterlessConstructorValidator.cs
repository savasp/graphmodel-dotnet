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
/// Validates that implementing classes have a parameterless constructor (GM002).
/// </summary>
internal class ParameterlessConstructorValidator : ITypeValidator
{
    public IEnumerable<Diagnostic> Validate(INamedTypeSymbol typeSymbol, SymbolAnalysisContext context)
    {
        if (!typeSymbol.IsValueType && !HasParameterlessConstructor(typeSymbol))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.MustHaveParameterlessConstructor,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name);
        }
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol typeSymbol)
    {
        var constructors = typeSymbol.Constructors;

        // If no explicit constructors, C# provides a default parameterless constructor
        if (!constructors.Any(c => !c.IsImplicitlyDeclared))
            return true;

        // Check for parameterless constructor (public or internal)
        return constructors.Any(c =>
            c.Parameters.Length == 0 &&
            (c.DeclaredAccessibility == Accessibility.Public ||
             c.DeclaredAccessibility == Accessibility.Internal));
    }
}
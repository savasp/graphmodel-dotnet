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
/// Validates constructor requirements for implementing types (GM001).
/// </summary>
internal class ConstructorValidator : ITypeValidator
{
    public IEnumerable<Diagnostic> Validate(INamedTypeSymbol typeSymbol, SymbolAnalysisContext context)
    {
        // Check if type has a parameterless constructor OR a constructor that initializes all properties
        if (!HasValidConstructor(typeSymbol))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.MustHaveParameterlessConstructorOrPropertyInitializer,
                typeSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name);
        }
    }

    private static bool HasValidConstructor(INamedTypeSymbol typeSymbol)
    {
        var constructors = typeSymbol.Constructors;

        // If no explicit constructors, C# provides a default parameterless constructor (valid)
        if (!constructors.Any(c => !c.IsImplicitlyDeclared))
            return true;

        // Check for parameterless constructor (public or internal)
        if (constructors.Any(c =>
            c.Parameters.Length == 0 &&
            (c.DeclaredAccessibility == Accessibility.Public ||
             c.DeclaredAccessibility == Accessibility.Internal)))
        {
            return true;
        }

        // Check for constructors that initialize all properties
        // For now, we'll be permissive and allow any constructor with parameters
        // as long as there are settable properties that could be initialized
        var settableProperties = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.SetMethod != null && 
                       (p.SetMethod.DeclaredAccessibility == Accessibility.Public ||
                        p.SetMethod.DeclaredAccessibility == Accessibility.Internal))
            .ToList();

        // If there are settable properties and at least one constructor, assume it's valid
        // This is a simplified check - in a real implementation, we'd need to analyze
        // the constructor body to ensure all properties are initialized
        return settableProperties.Any() && constructors.Any(c => c.Parameters.Length > 0);
    }
}
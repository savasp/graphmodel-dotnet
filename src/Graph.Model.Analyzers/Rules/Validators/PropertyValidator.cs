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
/// Validates properties in INode and IRelationship implementations.
/// </summary>
internal class PropertyValidator : ITypeValidator
{
    private readonly SupportedTypeChecker _typeChecker;
    private readonly ComplexTypeValidator _complexTypeValidator;

    public PropertyValidator()
    {
        _typeChecker = new SupportedTypeChecker();
        _complexTypeValidator = new ComplexTypeValidator();
    }

    public IEnumerable<Diagnostic> Validate(INamedTypeSymbol typeSymbol, SymbolAnalysisContext context)
    {
        // Remove the interface check entirely for now - validate ALL types
        // This will help us debug if the validator is running at all

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
            {
                // Skip inherited properties
                if (!property.ContainingType.Equals(typeSymbol, SymbolEqualityComparer.Default))
                    continue;

                // GM003: Check for public getters and setters
                foreach (var diagnostic in ValidatePropertyAccessors(property))
                {
                    yield return diagnostic;
                }

                // GM004 & GM005: Validate property type
                // Check if this type implements INode (using the same permissive check)
                var isNodeImplementation = typeSymbol.AllInterfaces.Any(i => i.Name == "INode");

                foreach (var diagnostic in ValidatePropertyType(property, isNodeImplementation))
                {
                    yield return diagnostic;
                }
            }
        }
    }

    private IEnumerable<Diagnostic> ValidatePropertyAccessors(IPropertySymbol property)
    {
        if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
            property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.PropertyMustHavePublicGetterAndSetter,
                property.Locations.FirstOrDefault(),
                property.Name);
        }
    }

    private IEnumerable<Diagnostic> ValidatePropertyType(IPropertySymbol property, bool isNodeImplementation)
    {
        var propertyType = property.Type;
        var typeLocation = GetPropertyTypeLocation(property);

        // First check if it's a supported simple type
        if (_typeChecker.IsSupportedSimpleType(propertyType))
        {
            yield break;
        }

        // For INode implementations only, check if it's a valid complex type
        if (isNodeImplementation && _complexTypeValidator.IsComplexType(propertyType))
        {
            var validation = _complexTypeValidator.ValidateComplexType(propertyType);
            if (!validation.IsValid)
            {
                yield return Diagnostic.Create(
                    DiagnosticDescriptors.InvalidComplexTypeProperty,
                    typeLocation ?? property.Locations.FirstOrDefault(),
                    property.Name,
                    propertyType.ToDisplayString());
            }
            // Important: If it's a valid complex type, don't report GM004
            yield break;
        }

        // If we get here, it's neither a supported simple type nor a valid complex type
        // This includes interfaces, delegates, unsupported generic types, etc.
        yield return Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedPropertyType,
            typeLocation ?? property.Locations.FirstOrDefault(),
            property.Name,
            propertyType.ToDisplayString());
    }

    private static Location? GetPropertyTypeLocation(IPropertySymbol property)
    {
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax propertySyntax)
        {
            return propertySyntax.Type.GetLocation();
        }
        return null;
    }
}
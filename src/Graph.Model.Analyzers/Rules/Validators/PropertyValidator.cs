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
    private readonly GraphDataModelChecker _typeChecker;

    public PropertyValidator()
    {
        _typeChecker = new GraphDataModelChecker();
    }

    public IEnumerable<Diagnostic> Validate(INamedTypeSymbol typeSymbol, SymbolAnalysisContext context)
    {
        // Determine if this type implements INode or IRelationship
        bool isNodeImplementation = typeSymbol.AllInterfaces.Any(i => i.Name == "INode");
        bool isRelationshipImplementation = typeSymbol.AllInterfaces.Any(i => i.Name == "IRelationship");

        if (!isNodeImplementation && !isRelationshipImplementation)
            yield break;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
            {
                // Skip inherited properties
                if (!property.ContainingType.Equals(typeSymbol, SymbolEqualityComparer.Default))
                    continue;

                // GM002: Check for public getters and setters/initializers
                foreach (var diagnostic in ValidatePropertyAccessors(property))
                {
                    yield return diagnostic;
                }

                // GM003: Check that properties are not INode or IRelationship
                foreach (var diagnostic in ValidatePropertyIsNotNodeOrRelationship(property))
                {
                    yield return diagnostic;
                }

                // GM004: Check complex properties don't have INode/IRelationship properties (recursive)
                if (isNodeImplementation)
                {
                    foreach (var diagnostic in ValidateComplexPropertyRecursive(property))
                    {
                        yield return diagnostic;
                    }
                }

                // GM005/GM006: Validate property types based on INode vs IRelationship
                if (isNodeImplementation)
                {
                    foreach (var diagnostic in ValidateNodePropertyType(property))
                    {
                        yield return diagnostic;
                    }
                }
                else if (isRelationshipImplementation)
                {
                    foreach (var diagnostic in ValidateRelationshipPropertyType(property))
                    {
                        yield return diagnostic;
                    }
                }
            }
        }
    }

    private IEnumerable<Diagnostic> ValidatePropertyAccessors(IPropertySymbol property)
    {
        // GM002: Must have public getter and either public setter or public initializer
        bool hasPublicGetter = property.GetMethod?.DeclaredAccessibility == Accessibility.Public;
        bool hasPublicSetter = property.SetMethod?.DeclaredAccessibility == Accessibility.Public;
        bool hasPublicInit = property.SetMethod?.IsInitOnly == true && property.SetMethod?.DeclaredAccessibility == Accessibility.Public;

        if (!hasPublicGetter || (!hasPublicSetter && !hasPublicInit))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.PropertyMustHavePublicAccessors,
                property.Locations.FirstOrDefault(),
                property.Name);
        }
    }

    private IEnumerable<Diagnostic> ValidatePropertyIsNotNodeOrRelationship(IPropertySymbol property)
    {
        // GM003: Properties cannot be INode or IRelationship or collections of them
        if (IsNodeOrRelationshipOrCollection(property.Type))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.PropertyCannotBeNodeOrRelationship,
                GetPropertyTypeLocation(property) ?? property.Locations.FirstOrDefault(),
                property.Name,
                property.Type.ToDisplayString());
        }
    }

    private IEnumerable<Diagnostic> ValidateComplexPropertyRecursive(IPropertySymbol property)
    {
        // GM004: Properties of complex properties cannot be INode or IRelationship (recursive)
        if (_typeChecker.IsComplex(property.Type))
        {
            if (HasInvalidNestedProperties(property.Type))
            {
                yield return Diagnostic.Create(
                    DiagnosticDescriptors.ComplexPropertyCannotHaveNodeOrRelationshipProperties,
                    GetPropertyTypeLocation(property) ?? property.Locations.FirstOrDefault(),
                    property.Name,
                    property.Type.ToDisplayString());
            }
        }
    }

    private IEnumerable<Diagnostic> ValidateNodePropertyType(IPropertySymbol property)
    {
        // GM005: INode properties must be simple, complex, or collections thereof (recursive)
        if (!IsValidNodePropertyType(property.Type))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.InvalidPropertyTypeForNode,
                GetPropertyTypeLocation(property) ?? property.Locations.FirstOrDefault(),
                property.Name,
                property.Type.ToDisplayString());
        }
    }

    private IEnumerable<Diagnostic> ValidateRelationshipPropertyType(IPropertySymbol property)
    {
        // GM006: IRelationship properties must be simple or collections of simple
        if (!IsValidRelationshipPropertyType(property.Type))
        {
            yield return Diagnostic.Create(
                DiagnosticDescriptors.InvalidPropertyTypeForRelationship,
                GetPropertyTypeLocation(property) ?? property.Locations.FirstOrDefault(),
                property.Name,
                property.Type.ToDisplayString());
        }
    }

    private bool IsNodeOrRelationshipOrCollection(ITypeSymbol type)
    {
        // Check the type itself
        if (_typeChecker.IsNodeOrRelationshipType(type))
            return true;

        // Check for arrays
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return _typeChecker.IsNodeOrRelationshipType(arrayType.ElementType);

        // Check for generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && _typeChecker.IsNodeOrRelationshipType(elementType);
        }

        return false;
    }

    private bool HasInvalidNestedProperties(ITypeSymbol type)
    {
        // Check for collections of complex types
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return HasInvalidNestedProperties(arrayType.ElementType);

        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && HasInvalidNestedProperties(elementType);
        }

        // For class types, check all properties recursively
        if (type.TypeKind == TypeKind.Class)
        {
            var properties = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public);

            foreach (var prop in properties)
            {
                // If any property is INode or IRelationship, it's invalid
                if (IsNodeOrRelationshipOrCollection(prop.Type))
                    return true;

                // Recursively check complex properties
                if (_typeChecker.IsComplex(prop.Type) && HasInvalidNestedProperties(prop.Type))
                    return true;
            }
        }

        return false;
    }

    private bool IsValidNodePropertyType(ITypeSymbol type)
    {
        // Must be simple, complex, or collections of simple/complex (recursive)
        return IsValidNodePropertyTypeRecursive(type);
    }

    private bool IsValidNodePropertyTypeRecursive(ITypeSymbol type)
    {
        // Simple types are always valid
        if (_typeChecker.IsSimple(type))
            return true;

        // Complex types are valid if they meet the requirements
        if (_typeChecker.IsComplex(type))
            return true;

        // Arrays of valid types are valid
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return IsValidNodePropertyTypeRecursive(arrayType.ElementType);

        // Generic collections of valid types are valid
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsValidNodePropertyTypeRecursive(elementType);
        }

        return false;
    }

    private bool IsValidRelationshipPropertyType(ITypeSymbol type)
    {
        // Must be simple or collections of simple only
        return IsValidRelationshipPropertyTypeRecursive(type);
    }

    private bool IsValidRelationshipPropertyTypeRecursive(ITypeSymbol type)
    {
        // Simple types are always valid
        if (_typeChecker.IsSimple(type))
            return true;

        // Arrays of simple types are valid
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return _typeChecker.IsSimple(arrayType.ElementType);

        // Generic collections of simple types are valid
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && _typeChecker.IsSimple(elementType);
        }

        return false;
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
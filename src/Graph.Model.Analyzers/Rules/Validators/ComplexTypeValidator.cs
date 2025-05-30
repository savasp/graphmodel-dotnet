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

namespace Cvoya.Graph.Model.Analyzers.Rules.Validators;

/// <summary>
/// Validates complex types used in INode implementations (GM005).
/// </summary>
internal class ComplexTypeValidator
{
    private readonly SupportedTypeChecker _typeChecker;

    public ComplexTypeValidator()
    {
        _typeChecker = new SupportedTypeChecker();
    }

    public bool IsComplexType(ITypeSymbol type)
    {
        // Arrays and collections of user-defined classes are considered complex types
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsUserDefinedClassType(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol { IsGenericType: true } genericType && IsCollectionType(genericType))
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsUserDefinedClassType(elementType);
        }

        // Single user-defined class types
        return IsUserDefinedClassType(type);
    }

    public ComplexTypeValidationResult ValidateComplexType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return ValidateSingleClassType(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol { IsGenericType: true } genericType && IsCollectionType(genericType))
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null ? ValidateSingleClassType(elementType) :
                new ComplexTypeValidationResult(false, "Unknown element type");
        }

        return ValidateSingleClassType(type);
    }

    public ComplexTypeValidationResult ValidateSingleClassType(ITypeSymbol type)
    {
        if (!IsUserDefinedClassType(type))
        {
            return new ComplexTypeValidationResult(false, "Type must be a user-defined class");
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return new ComplexTypeValidationResult(false, "Type must be a named type");
        }

        if (!HasParameterlessConstructor(namedType))
        {
            return new ComplexTypeValidationResult(false,
                "Complex types must have a parameterless constructor");
        }

        // Validate all properties
        foreach (var member in namedType.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                // Check if property is public
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    return new ComplexTypeValidationResult(false,
                        $"Property '{property.Name}' must be public");
                }

                // Check if property has both public getter and setter
                if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                    property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                {
                    return new ComplexTypeValidationResult(false,
                        $"Property '{property.Name}' must have public getter and setter");
                }

                // Check if property type is supported (simple type or valid complex type)
                if (!_typeChecker.IsSupportedSimpleType(property.Type) &&
                    !IsValidComplexType(property.Type))
                {
                    return new ComplexTypeValidationResult(false,
                        $"Property '{property.Name}' must be a simple supported type or valid complex type");
                }
            }
        }

        return new ComplexTypeValidationResult(true);
    }

    private bool IsValidComplexType(ITypeSymbol type)
    {
        // Recursively validate nested complex types
        var result = ValidateSingleClassType(type);
        return result.IsValid;
    }

    private static bool IsUserDefinedClassType(ITypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.SpecialType != SpecialType.None)
            return false;

        var typeName = type.ToDisplayString();

        // Exclude string
        if (typeName == "string")
            return false;

        // Exclude system and framework types
        var typeNamespace = type.ContainingNamespace?.ToDisplayString() ?? "";
        return !typeNamespace.StartsWith("System") &&
               !typeNamespace.StartsWith("Microsoft") &&
               !typeNamespace.StartsWith("NetTopologySuite");
    }

    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        var typeName = type.ConstructedFrom.Name;
        var typeNamespace = type.ConstructedFrom.ContainingNamespace?.ToDisplayString();

        return typeNamespace == "System.Collections.Generic" &&
               typeName is "List" or "HashSet" or "IList" or "ICollection" or "IEnumerable";
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol type)
    {
        var constructors = type.Constructors.Where(c => !c.IsStatic).ToList();

        // If no constructors, compiler provides default
        if (!constructors.Any())
            return true;

        // Check for parameterless constructor
        return constructors.Any(c => !c.Parameters.Any() &&
            (c.DeclaredAccessibility == Accessibility.Public ||
             c.DeclaredAccessibility == Accessibility.Internal));
    }
}
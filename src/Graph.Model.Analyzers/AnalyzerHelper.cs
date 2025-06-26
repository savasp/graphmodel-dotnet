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

namespace Cvoya.Graph.Model.Analyzers;

using Microsoft.CodeAnalysis;


/// <summary>
/// Helper class for type checking and validation in the Graph Model analyzer.
/// </summary>
internal class AnalyzerHelper
{
    private readonly Compilation _compilation;

    public AnalyzerHelper(Compilation compilation)
    {
        _compilation = compilation;
    }

    public bool ImplementsINode(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.Model.INode");
    }

    public bool ImplementsIRelationship(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.Model.IRelationship");
    }

    private bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == interfaceName);
    }

    public bool IsGraphInterfaceType(ITypeSymbol type)
    {
        // Handle nullable types
        if (type is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsGraphInterfaceType(namedType.TypeArguments[0]);
        }

        // Check if type IS INode or IRelationship interface directly
        if (type is INamedTypeSymbol namedTypeSymbol)
        {
            var namespaceName = namedTypeSymbol.ContainingNamespace?.ToDisplayString();
            var typeName = namedTypeSymbol.Name;

            if (namespaceName == "Cvoya.Graph.Model" &&
                (typeName == "INode" || typeName == "IRelationship"))
            {
                return true;
            }

            // Check if type implements INode or IRelationship
            if (ImplementsINode(namedTypeSymbol) || ImplementsIRelationship(namedTypeSymbol))
                return true;
        }

        // Check collections of INode or IRelationship
        if (IsCollectionType(type))
        {
            var elementType = GetCollectionElementType(type);
            if (elementType != null && IsGraphInterfaceType(elementType))
                return true;
        }

        return false;
    }

    public bool IsValidNodePropertyType(ITypeSymbol type)
    {
        // INode can have: simple types, complex types, collections of simple types, collections of complex types

        // First check if it's a graph interface type (not allowed)
        if (IsGraphInterfaceType(type))
            return false;

        // Check for unsupported framework types early
        if (IsUnsupportedFrameworkType(type))
            return false;

        // Check collections of unsupported framework types early
        if (IsCollectionType(type))
        {
            var elementType = GetCollectionElementType(type);
            if (elementType != null && IsUnsupportedFrameworkType(elementType))
                return false;
        }

        // Check if it's a simple type
        if (IsSimpleType(type))
            return true;

        // Check if it's a collection of simple types
        if (IsCollectionOfSimpleTypes(type))
            return true;

        // Check if it's a complex type
        if (IsComplexType(type))
        {
            var result = ValidateComplexType(type);
            return result.IsValid;
        }

        // Check if it's a collection of complex types
        if (IsCollectionOfComplexTypes(type))
        {
            var elementType = GetCollectionElementType(type);
            if (elementType != null && IsComplexType(elementType))
            {
                var result = ValidateComplexType(elementType);
                return result.IsValid;
            }
        }

        return false;
    }

    private bool IsUnsupportedFrameworkType(ITypeSymbol type)
    {
        // Check specific unsupported framework types by name and namespace
        var fullName = type.ToDisplayString();

        // Task and related types
        if (fullName.StartsWith("System.Threading.Tasks."))
            return true;

        // Delegates and actions
        if (fullName.StartsWith("System.Action") || fullName.StartsWith("System.Func"))
            return true;

        // Check if it's a delegate type
        if (type.TypeKind == TypeKind.Delegate)
            return true;

        // Other common unsupported types that might cause issues
        if (fullName.StartsWith("System.IO.") ||
            fullName.StartsWith("System.Net.") ||
            fullName.StartsWith("System.Reflection.") ||
            fullName.StartsWith("System.Runtime."))
            return true;

        return false;
    }

    public bool IsValidRelationshipPropertyType(ITypeSymbol type)
    {
        // IRelationship can only have: simple types or collections of simple types

        // First check if it's a graph interface type (not allowed)
        if (IsGraphInterfaceType(type))
            return false;

        // Check for unsupported framework types early
        if (IsUnsupportedFrameworkType(type))
            return false;

        // Check collections of unsupported framework types early
        if (IsCollectionType(type))
        {
            var elementType = GetCollectionElementType(type);
            if (elementType != null && IsUnsupportedFrameworkType(elementType))
                return false;
        }

        // Check if it's a simple type
        if (IsSimpleType(type))
            return true;

        // Check if it's a collection of simple types
        if (IsCollectionOfSimpleTypes(type))
            return true;

        return false;
    }

    public bool IsSimpleType(ITypeSymbol type)
    {
        // Handle nullable types first
        if (type is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsSimpleType(namedType.TypeArguments[0]);
        }

        // Check primitive types
        if (type.SpecialType is
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Decimal or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Char or
            SpecialType.System_String)
        {
            return true;
        }

        // Check enums
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check specific types by full name - matching Graph.Model logic exactly
        var fullName = type.ToDisplayString();
        return fullName switch
        {
            "System.Drawing.Point" => true,
            "System.DateTime" => true,
            "System.DateTimeOffset" => true,
            "System.TimeSpan" => true,
            "System.TimeOnly" => true,
            "System.DateOnly" => true,
            "System.Guid" => true,
            "byte[]" => true,
            "System.Uri" => true,
            "Cvoya.Graph.Model.Point" => true,
            _ => false
        };
    }

    public bool IsComplexType(ITypeSymbol type)
    {
        // A complex type is one that is not simple and not a collection
        if (IsSimpleType(type))
            return false;

        if (IsCollectionOfSimpleTypes(type))
            return false;

        if (IsCollectionOfComplexTypes(type))
            return false;

        // Must be a reference type (class or struct) to be complex
        if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            return false;

        return true;
    }

    public bool IsCollectionType(ITypeSymbol type)
    {
        return IsCollectionOfSimpleTypes(type) || IsCollectionOfComplexTypes(type);
    }

    public bool IsCollectionOfSimpleTypes(ITypeSymbol type)
    {
        // Exclude string (even though it implements IEnumerable)
        if (type.SpecialType == SpecialType.System_String)
            return false;

        // Check if it implements IEnumerable
        if (!ImplementsIEnumerable(type))
            return false;

        // Check arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSimpleType(arrayType.ElementType);
        }

        // Check generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsSimpleType(elementType);
        }

        return false;
    }

    public bool IsCollectionOfComplexTypes(ITypeSymbol type)
    {
        // Exclude string (even though it implements IEnumerable)
        if (type.SpecialType == SpecialType.System_String)
            return false;

        // Check if it implements IEnumerable
        if (!ImplementsIEnumerable(type))
            return false;

        // Check arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsComplexType(arrayType.ElementType);
        }

        // Check generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsComplexType(elementType);
        }

        return false;
    }

    private bool ImplementsIEnumerable(ITypeSymbol type)
    {
        // Arrays implement IEnumerable
        if (type is IArrayTypeSymbol)
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            // Check if it directly is IEnumerable or IEnumerable<T>
            if (namedType.Name == "IEnumerable" &&
                namedType.ContainingNamespace?.ToDisplayString() is "System.Collections" or "System.Collections.Generic")
            {
                return true;
            }

            // Check implemented interfaces
            foreach (var interfaceType in namedType.AllInterfaces)
            {
                if (interfaceType.Name == "IEnumerable" &&
                    interfaceType.ContainingNamespace?.ToDisplayString() is "System.Collections" or "System.Collections.Generic")
                {
                    return true;
                }
            }
        }

        return false;
    }

    public ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        // Handle arrays
        if (type is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // Handle generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            return genericType.TypeArguments.FirstOrDefault();
        }

        return null;
    }

    public ComplexTypeValidationResult ValidateComplexType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return new ComplexTypeValidationResult(false, "Type is not a named type");

        // Check all properties recursively
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return ValidateComplexTypeRecursive(namedType, visited);
    }

    private ComplexTypeValidationResult ValidateComplexTypeRecursive(INamedTypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (visited.Contains(type))
            return new ComplexTypeValidationResult(true, null); // Avoid infinite recursion

        visited.Add(type);

        var properties = type.GetMembers().OfType<IPropertySymbol>();
        foreach (var property in properties)
        {
            // Check if property is a graph interface type
            if (IsGraphInterfaceType(property.Type))
            {
                return new ComplexTypeValidationResult(false, $"Property {property.Name} is a graph interface type");
            }

            // Check collections recursively
            if (IsCollectionType(property.Type))
            {
                var elementType = GetCollectionElementType(property.Type);
                if (elementType != null && IsGraphInterfaceType(elementType))
                {
                    return new ComplexTypeValidationResult(false, $"Property {property.Name} is a collection of graph interface types");
                }
            }

            // Check nested complex types recursively
            if (IsComplexType(property.Type))
            {
                var result = ValidateComplexTypeRecursive((INamedTypeSymbol)property.Type, visited);
                if (!result.IsValid)
                {
                    return result;
                }
            }
            else if (IsCollectionOfComplexTypes(property.Type))
            {
                var elementType = GetCollectionElementType(property.Type);
                if (elementType is INamedTypeSymbol namedElementType)
                {
                    var result = ValidateComplexTypeRecursive(namedElementType, visited);
                    if (!result.IsValid)
                    {
                        return result;
                    }
                }
            }
        }

        return new ComplexTypeValidationResult(true, null);
    }

    public bool IsNullableType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
    }
}

/// <summary>
/// Result of complex type validation.
/// </summary>
internal class ComplexTypeValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public ComplexTypeValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}
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
using System.Collections.Immutable;

namespace Cvoya.Graph.Model.Analyzers.Rules;

/// <summary>
/// Helper methods for type analysis in Graph.Model analyzers.
/// </summary>
public static class TypeHelpers
{
    /// <summary>
    /// Supported primitive type names.
    /// </summary>
    private static readonly ImmutableHashSet<string> SupportedPrimitiveTypes = ImmutableHashSet.Create(
        "System.Boolean", "System.Byte", "System.SByte", "System.Char",
        "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
        "System.Int64", "System.UInt64", "System.Single", "System.Double"
    );

    /// <summary>
    /// Supported date/time type names.
    /// </summary>
    private static readonly ImmutableHashSet<string> SupportedDateTimeTypes = ImmutableHashSet.Create(
        "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", 
        "System.DateOnly", "System.TimeOnly"
    );

    /// <summary>
    /// Other supported simple type names.
    /// </summary>
    private static readonly ImmutableHashSet<string> SupportedSimpleTypes = ImmutableHashSet.Create(
        "System.String", "System.Decimal", "System.Guid"
    );

    /// <summary>
    /// Checks if a type implements the specified interface.
    /// </summary>
    public static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == interfaceName);
    }

    /// <summary>
    /// Checks if a type implements INode interface.
    /// </summary>
    public static bool ImplementsINode(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.Model.INode");
    }

    /// <summary>
    /// Checks if a type implements IRelationship interface.
    /// </summary>
    public static bool ImplementsIRelationship(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.Model.IRelationship");
    }

    /// <summary>
    /// Checks if a type is a simple/primitive type that's supported by the graph model.
    /// </summary>
    public static bool IsSimpleType(ITypeSymbol type)
    {
        if (type == null) return false;

        var typeName = type.ToDisplayString();

        // Check primitives
        if (SupportedPrimitiveTypes.Contains(typeName))
            return true;

        // Check date/time types
        if (SupportedDateTimeTypes.Contains(typeName))
            return true;

        // Check other simple types
        if (SupportedSimpleTypes.Contains(typeName))
            return true;

        // Check enums
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check Point type
        if (typeName == "Cvoya.Graph.Model.Point")
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a type is a collection of simple types.
    /// </summary>
    public static bool IsCollectionOfSimpleTypes(ITypeSymbol type)
    {
        if (type == null) return false;

        // Arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSimpleType(arrayType.ElementType);
        }

        // Generic collections (IEnumerable<T>, List<T>, etc.)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericType = namedType.ConstructedFrom.ToDisplayString();
            
            // Check if it's a collection type
            if (genericType.StartsWith("System.Collections.Generic.IEnumerable<") ||
                genericType.StartsWith("System.Collections.Generic.List<") ||
                genericType.StartsWith("System.Collections.Generic.IList<") ||
                genericType.StartsWith("System.Collections.Generic.ICollection<"))
            {
                var elementType = namedType.TypeArguments.FirstOrDefault();
                return elementType != null && IsSimpleType(elementType);
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is a valid complex type for INode implementations.
    /// A valid complex type is a class with a parameterless constructor and only simple properties.
    /// </summary>
    public static bool IsValidComplexType(INamedTypeSymbol type, out string? errorReason)
    {
        errorReason = null;

        if (type == null)
        {
            errorReason = "Type is null";
            return false;
        }

        // Must be a class
        if (type.TypeKind != TypeKind.Class)
        {
            errorReason = "Must be a class";
            return false;
        }

        // Must have parameterless constructor
        if (!HasParameterlessConstructor(type))
        {
            errorReason = "Must have a parameterless constructor";
            return false;
        }

        // All properties must be simple types or collections of simple types
        foreach (var member in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (!IsSimpleType(member.Type) && !IsCollectionOfSimpleTypes(member.Type))
            {
                errorReason = $"Property '{member.Name}' has unsupported type '{member.Type.ToDisplayString()}'";
                return false;
            }

            // Must have public getter and setter
            if (member.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                member.SetMethod?.DeclaredAccessibility != Accessibility.Public)
            {
                errorReason = $"Property '{member.Name}' must have public getter and setter";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a type is a collection of valid complex types.
    /// </summary>
    public static bool IsCollectionOfValidComplexTypes(ITypeSymbol type, out string? errorReason)
    {
        errorReason = null;

        if (type == null)
        {
            errorReason = "Type is null";
            return false;
        }

        // Arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            if (arrayType.ElementType is INamedTypeSymbol elementNamedType)
            {
                return IsValidComplexType(elementNamedType, out errorReason);
            }
            errorReason = "Array element type is not a named type";
            return false;
        }

        // Generic collections
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericType = namedType.ConstructedFrom.ToDisplayString();
            
            if (genericType.StartsWith("System.Collections.Generic.IEnumerable<") ||
                genericType.StartsWith("System.Collections.Generic.List<") ||
                genericType.StartsWith("System.Collections.Generic.IList<") ||
                genericType.StartsWith("System.Collections.Generic.ICollection<"))
            {
                var elementType = namedType.TypeArguments.FirstOrDefault();
                if (elementType is INamedTypeSymbol elementNamedType)
                {
                    return IsValidComplexType(elementNamedType, out errorReason);
                }
                errorReason = "Collection element type is not a named type";
                return false;
            }
        }

        errorReason = "Not a supported collection type";
        return false;
    }

    /// <summary>
    /// Checks if a type has a parameterless constructor.
    /// </summary>
    public static bool HasParameterlessConstructor(INamedTypeSymbol type)
    {
        var constructors = type.Constructors;
        
        // If no explicit constructors, there's an implicit parameterless constructor
        if (!constructors.Any())
            return true;

        // Check for explicit parameterless constructor (public or internal)
        return constructors.Any(c => 
            c.Parameters.Length == 0 && 
            (c.DeclaredAccessibility == Accessibility.Public || 
             c.DeclaredAccessibility == Accessibility.Internal));
    }

    /// <summary>
    /// Gets the appropriate interface name for a type (INode or IRelationship).
    /// </summary>
    public static string? GetGraphInterfaceName(INamedTypeSymbol type)
    {
        if (ImplementsINode(type))
            return "INode";
        if (ImplementsIRelationship(type))
            return "IRelationship";
        return null;
    }
}
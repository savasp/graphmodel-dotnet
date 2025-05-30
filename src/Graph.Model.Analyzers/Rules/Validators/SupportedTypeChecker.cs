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
/// Checks if types are supported for graph model properties.
/// </summary>
internal class SupportedTypeChecker
{
    private static readonly HashSet<string> SupportedTypes =
    [
        "System.Guid",
        "System.Uri",
        "NetTopologySuite.Geometries.Point",
        "Cvoya.Graph.Model.Point", // Add Graph.Model Point type
        "Point" // Also check for just "Point" in case namespace is implied
    ];

    public bool IsSupportedSimpleType(ITypeSymbol type)
    {
        // Handle nullable types
        if (type is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsSupportedSimpleType(namedType.TypeArguments[0]);
        }

        // Explicitly reject non-generic collection types from System.Collections
        if (type is INamedTypeSymbol { IsGenericType: false } nonGenericType)
        {
            var namespaceName = nonGenericType.ContainingNamespace?.ToDisplayString();

            if (namespaceName == "System.Collections" &&
                nonGenericType.Name is "IEnumerable" or "ICollection" or "IList")
            {
                return false;
            }
        }

        // Check specific primitive types we support (exclude object and other unsupported types)
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

        // Check enums - they're always supported
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check date/time types
        var fullName = type.ToDisplayString();
        if (IsDateTimeType(fullName))
            return true;

        // Check other supported types
        if (IsSupportedType(fullName) || IsSupportedType(type.Name))
            return true;

        // Check collection types - both interfaces and concrete types
        if (type is INamedTypeSymbol { IsGenericType: true } genericType && IsValidGenericCollectionType(genericType))
            return true;

        // Check for single-dimensional arrays
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return IsSupportedSimpleType(arrayType.ElementType);

        return false;
    }

    private bool IsValidGenericCollectionType(INamedTypeSymbol genericType)
    {
        // Must be generic
        if (!genericType.IsGenericType)
            return false;

        var typeName = genericType.ConstructedFrom.Name;
        var typeNamespace = genericType.ConstructedFrom.ContainingNamespace?.ToDisplayString();

        // Only support generic collections from System.Collections.Generic
        if (typeNamespace != "System.Collections.Generic")
            return false;

        // Check if it's a supported collection type (including interfaces)
        if (!IsCollectionType(typeName))
            return false;

        // Validate element type
        var elementType = genericType.TypeArguments.FirstOrDefault();
        return elementType != null && IsSupportedSimpleType(elementType);
    }

    private static bool IsDateTimeType(string typeName)
    {
        return typeName is "System.DateTime" or "System.DateTimeOffset"
            or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly"
            or "DateTime" or "DateTimeOffset" or "TimeSpan" or "DateOnly" or "TimeOnly";
    }

    private static bool IsSupportedType(string typeName)
    {
        return SupportedTypes.Contains(typeName);
    }

    private bool IsValidCollectionType(ITypeSymbol type)
    {
        // Check for arrays - only single-dimensional arrays are supported
        if (type is IArrayTypeSymbol arrayType)
        {
            // Reject multidimensional arrays
            if (arrayType.Rank > 1)
                return false;

            return IsSupportedSimpleType(arrayType.ElementType);
        }

        // Generic collections
        if (type is INamedTypeSymbol { IsGenericType: true } genericType)
        {
            var typeName = genericType.ConstructedFrom.Name;
            var typeNamespace = genericType.ConstructedFrom.ContainingNamespace?.ToDisplayString();

            // Check if it's a supported collection type
            if ((typeNamespace == "System.Collections.Generic" &&
                 IsCollectionType(typeName)) ||
                typeName is "IEnumerable" or "ICollection" or "IList")
            {
                // Validate element type
                var elementType = genericType.TypeArguments.FirstOrDefault();
                return elementType != null && IsSupportedSimpleType(elementType);
            }
        }

        return false;
    }

    private static bool IsCollectionType(string typeName)
    {
        return typeName is "List" or "HashSet" or "SortedSet" or "LinkedList"
            or "Queue" or "Stack" or "IList" or "ICollection" or "IEnumerable"
            or "IReadOnlyCollection" or "IReadOnlyList" or "ISet" or "IReadOnlySet";
    }
}
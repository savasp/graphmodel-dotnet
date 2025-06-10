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
/// Provides predicates for determining simple and complex types in the graph data model
/// for use in Roslyn analyzers (works with ITypeSymbol instead of Type).
/// </summary>
internal class GraphDataModelChecker
{
    private static readonly HashSet<string> SupportedSimpleTypeNames =
    [
        "System.Boolean", "System.Nullable<System.Boolean>",
        "System.Byte", "System.Nullable<System.Byte>",
        "System.SByte", "System.Nullable<System.SByte>",
        "System.Int16", "System.Nullable<System.Int16>",
        "System.UInt16", "System.Nullable<System.UInt16>",
        "System.Int32", "System.Nullable<System.Int32>",
        "System.UInt32", "System.Nullable<System.UInt32>",
        "System.Int64", "System.Nullable<System.Int64>",
        "System.UInt64", "System.Nullable<System.UInt64>",
        "System.Decimal", "System.Nullable<System.Decimal>",
        "System.Single", "System.Nullable<System.Single>",
        "System.Double", "System.Nullable<System.Double>",
        "System.Char", "System.Nullable<System.Char>",
        "System.String",
        "System.DateTime", "System.Nullable<System.DateTime>",
        "System.DateTimeOffset", "System.Nullable<System.DateTimeOffset>",
        "System.TimeSpan", "System.Nullable<System.TimeSpan>",
        "System.DateOnly", "System.Nullable<System.DateOnly>",
        "System.TimeOnly", "System.Nullable<System.TimeOnly>",
        "System.Guid", "System.Nullable<System.Guid>",
        "System.Uri",
        "Cvoya.Graph.Model.Point", "System.Nullable<Cvoya.Graph.Model.Point>",
        // Also support short names
        "bool", "bool?",
        "byte", "byte?",
        "sbyte", "sbyte?",
        "short", "short?",
        "ushort", "ushort?",
        "int", "int?",
        "uint", "uint?",
        "long", "long?",
        "ulong", "ulong?",
        "decimal", "decimal?",
        "float", "float?",
        "double", "double?",
        "char", "char?",
        "string",
        "DateTime", "DateTime?",
        "DateTimeOffset", "DateTimeOffset?",
        "TimeSpan", "TimeSpan?",
        "DateOnly", "DateOnly?",
        "TimeOnly", "TimeOnly?",
        "Guid", "Guid?",
        "Uri",
        "Point", "Point?"
    ];

    /// <summary>
    /// Determines if a type is a simple type supported by the graph data model.
    /// </summary>
    public bool IsSimple(ITypeSymbol type)
    {
        // Handle nullable types
        if (type is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsSimple(namedType.TypeArguments[0]);
        }

        // Check for supported primitive types
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

        // Check for enums
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check known simple types by name
        var fullName = type.ToDisplayString();
        if (SupportedSimpleTypeNames.Contains(fullName) || SupportedSimpleTypeNames.Contains(type.Name))
            return true;

        // Check for single-dimensional arrays of simple types
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
            return IsSimple(arrayType.ElementType);

        // Check for generic collections of simple types
        if (type is INamedTypeSymbol { IsGenericType: true } genericType && IsCollectionType(genericType))
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsSimple(elementType);
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a complex type supported by the graph data model.
    /// </summary>
    public bool IsComplex(ITypeSymbol type)
    {
        // Simple types are not complex
        if (IsSimple(type))
            return false;

        // Check for collections of complex types
        if (type is IArrayTypeSymbol { Rank: 1 } arrayType)
        {
            return IsComplex(arrayType.ElementType);
        }

        if (type is INamedTypeSymbol { IsGenericType: true } genericType && IsCollectionType(genericType))
        {
            var elementType = genericType.TypeArguments.FirstOrDefault();
            return elementType != null && IsComplex(elementType);
        }

        // Must be a class (not struct, interface, abstract class, etc.)
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            return false;

        // Cannot be INode or IRelationship
        if (IsNodeOrRelationshipType(type))
            return false;

        // Must have a parameterless constructor
        if (type is INamedTypeSymbol classType && !HasParameterlessConstructor(classType))
            return false;

        // All properties must be simple types
        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                       p.GetMethod != null && p.SetMethod != null);

        return properties.All(p => IsSimple(p.Type));
    }

    /// <summary>
    /// Checks if a type implements INode or IRelationship.
    /// </summary>
    public bool IsNodeOrRelationshipType(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i => 
            i.Name == "INode" || 
            i.Name == "IRelationship");
    }

    private static bool IsCollectionType(INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return false;

        var typeName = type.ConstructedFrom.Name;
        var typeNamespace = type.ConstructedFrom.ContainingNamespace?.ToDisplayString();

        // Only support generic collections from System.Collections.Generic
        if (typeNamespace != "System.Collections.Generic")
            return false;

        return typeName is "List" or "HashSet" or "SortedSet" or "LinkedList"
            or "Queue" or "Stack" or "IList" or "ICollection" or "IEnumerable"
            or "IReadOnlyCollection" or "IReadOnlyList" or "ISet" or "IReadOnlySet";
    }

    private static bool HasParameterlessConstructor(INamedTypeSymbol type)
    {
        var constructors = type.Constructors;

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
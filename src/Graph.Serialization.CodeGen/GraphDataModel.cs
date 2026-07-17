// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Linq;
using Microsoft.CodeAnalysis;


internal static class GraphDataModel
{
    // Use SpecialType enum for built-in types - much more reliable!
    private static readonly SpecialType[] SimpleSpecialTypes =
    [
        SpecialType.System_String,
        SpecialType.System_Int32,
        SpecialType.System_Int64,
        SpecialType.System_Single,
        SpecialType.System_Double,
        SpecialType.System_Decimal,
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_UInt32,
        SpecialType.System_UInt64,
        SpecialType.System_Char,
        SpecialType.System_DateTime,
    ];

    // For types that don't have SpecialType representations, we need to check by metadata
    private static readonly (string Namespace, string TypeName)[] AdditionalSimpleTypes =
    [
        ("System", "DateTimeOffset"),
        ("System", "Guid"),
        ("System", "TimeSpan"),
        ("System", "DateOnly"),
        ("System", "TimeOnly"),
        ("System", "Uri"),
        ("Cvoya.Graph", "Point")
    ];

    public static bool IsSimple(ITypeSymbol type)
    {
        // Handle nullable value types
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            type = namedType.TypeArguments[0];
        }

        // Check if it's an enum type (align with runtime version)
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check if it's a special type (much more reliable than string comparison!)
        if (SimpleSpecialTypes.Contains(type.SpecialType))
            return true;

        // For byte arrays, check specifically
        if (type is IArrayTypeSymbol arrayType &&
            arrayType.ElementType.SpecialType == SpecialType.System_Byte)
            return true;

        // Check additional types by namespace and name
        if (type is INamedTypeSymbol named)
        {
            var namespaceName = named.ContainingNamespace?.ToString();
            var typeName = named.Name;

            return AdditionalSimpleTypes.Any(t =>
                t.Namespace == namespaceName &&
                t.TypeName == typeName);
        }

        return false;
    }

    public static bool IsCollectionOfSimple(ITypeSymbol type)
    {
        // String is not considered a collection, even though it implements IEnumerable<char>
        if (type.SpecialType == SpecialType.System_String)
            return false;

        // Handle arrays first
        if (type is IArrayTypeSymbol arrayType)
        {
            return IsSimple(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check if it implements IEnumerable<T>
        var enumerableInterface = namedType.AllInterfaces
            .FirstOrDefault(i =>
                i.IsGenericType &&
                i.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

        if (enumerableInterface != null)
        {
            var elementType = enumerableInterface.TypeArguments.FirstOrDefault();
            return elementType != null && IsSimple(elementType);
        }

        // Check if the type itself is IEnumerable<T>
        if (namedType.IsGenericType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            var elementType = namedType.TypeArguments.FirstOrDefault();
            return elementType != null && IsSimple(elementType);
        }

        return false;
    }

    internal static bool IsCollectionOfComplex(ITypeSymbol type)
    {
        // String is not considered a collection, even though it implements IEnumerable<char>
        if (type.SpecialType == SpecialType.System_String)
            return false;

        var elementType = GetCollectionElementType(type);
        return elementType != null && !IsSimple(elementType);
    }

    /// <summary>
    /// Classifies how the code generator must construct a value for a collection-shaped property or
    /// parameter so that the result is assignable to the declared type. This is the single
    /// construction matrix shared with the analyzer (mirrored by
    /// <c>Cvoya.Graph.Analyzers.AnalyzerHelper.IsConstructibleCollectionType</c>): only the shapes
    /// enumerated here are code-generable, and the analyzer rejects everything else via CG004/CG005.
    /// </summary>
    internal static CollectionConstructionKind GetCollectionConstructionKind(ITypeSymbol type)
    {
        // String enumerates chars but is never a collection property here.
        if (type.SpecialType == SpecialType.System_String)
            return CollectionConstructionKind.None;

        if (type is IArrayTypeSymbol)
            return CollectionConstructionKind.Array;

        if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
            return CollectionConstructionKind.None;

        var definition = namedType.ConstructedFrom;

        // A List<T> instance is assignable to each of these read/mutable sequence interfaces, so
        // they are all constructed as List<T>.
        switch (definition.SpecialType)
        {
            case SpecialType.System_Collections_Generic_IEnumerable_T:
            case SpecialType.System_Collections_Generic_ICollection_T:
            case SpecialType.System_Collections_Generic_IList_T:
            case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
            case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                return CollectionConstructionKind.List;
        }

        // ISet<T>/IReadOnlySet<T> have no SpecialType, so match by name. HashSet<T> satisfies both,
        // so all three are constructed as HashSet<T>. Concrete List<T>/HashSet<T> match here too.
        if (definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic")
        {
            return definition.Name switch
            {
                "List" => CollectionConstructionKind.List,
                "HashSet" => CollectionConstructionKind.Set,
                "ISet" => CollectionConstructionKind.Set,
                "IReadOnlySet" => CollectionConstructionKind.Set,
                _ => CollectionConstructionKind.None,
            };
        }

        // Any other concrete/custom collection (Queue<T>, SortedSet<T>, ObservableCollection<T>, ...)
        // cannot be constructed by assigning a List<T>/HashSet<T>/array, so it is unsupported.
        return CollectionConstructionKind.None;
    }

    internal static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
    {
        // String is not considered a collection, even though it implements IEnumerable<char>
        if (type.SpecialType == SpecialType.System_String)
            return null;

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType;
        }

        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var enumerableInterface = namedType.AllInterfaces
                .FirstOrDefault(i => i.IsGenericType &&
                                     i.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

            if (enumerableInterface != null)
            {
                return enumerableInterface.TypeArguments.FirstOrDefault();
            }

            if (namedType.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return namedType.TypeArguments.FirstOrDefault();
            }
        }

        return null;
    }
}
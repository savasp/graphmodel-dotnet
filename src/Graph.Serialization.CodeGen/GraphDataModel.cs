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

        // Check additional types by metadata and defining-assembly identity. Consumer source can
        // legally shadow these fully-qualified names, so namespace/name text alone is insufficient.
        if (type is INamedTypeSymbol named)
        {
            if (named.ContainingNamespace?.ToDisplayString() == "System" &&
                named.MetadataName is "DateTimeOffset" or "Guid" or "TimeSpan" or "DateOnly" or
                    "TimeOnly" or "Uri")
            {
                return IsFrameworkAssembly(named.ContainingAssembly);
            }

            return named.MetadataName == "Point" &&
                   named.ContainingNamespace?.ToDisplayString() == "Cvoya.Graph" &&
                   named.ContainingAssembly.Identity.Name == "Cvoya.Graph";
        }

        return false;
    }

    public static bool IsCollectionOfSimple(ITypeSymbol type)
    {
        // String is not considered a collection, even though it implements IEnumerable<char>
        if (type.SpecialType == SpecialType.System_String)
            return false;

        if (IsDictionaryType(type))
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

        if (IsDictionaryType(type))
            return false;

        var elementType = GetCollectionElementType(type);
        return elementType != null && !IsSimple(elementType);
    }

    internal static bool IsUnsupportedFrameworkType(ITypeSymbol type)
    {
        type = UnwrapNullableValueType(type);

        if (type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
            return true;

        // Keep source-defined lookalikes on the unsupported path instead of treating them as
        // complex values. Runtime simple-type classification uses exact CLR Type identity (#426).
        if (IsNamedSimpleTypeLookalike(type))
            return true;

        var fullName = type.ToDisplayString();
        return fullName.StartsWith("System.Threading.Tasks.") ||
               fullName.StartsWith("System.Action") ||
               fullName.StartsWith("System.Func") ||
               type.TypeKind == TypeKind.Delegate ||
               fullName.StartsWith("System.IO.") ||
               fullName.StartsWith("System.Net.") ||
               fullName.StartsWith("System.Reflection.") ||
               fullName.StartsWith("System.Runtime.");
    }

    private static bool IsNamedSimpleTypeLookalike(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        if (named.ContainingNamespace?.ToDisplayString() == "System" &&
            named.MetadataName is "DateTime" or "DateTimeOffset" or "TimeSpan" or "DateOnly" or
                "TimeOnly" or "Guid" or "Uri")
        {
            return !IsFrameworkAssembly(named.ContainingAssembly);
        }

        return named.MetadataName == "Point" &&
               named.ContainingNamespace?.ToDisplayString() == "Cvoya.Graph" &&
               named.ContainingAssembly.Identity.Name != "Cvoya.Graph";
    }

    internal static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        if (namedType.Name == "IDictionary" &&
            namedType.ContainingNamespace?.ToDisplayString() == "System.Collections")
        {
            return true;
        }

        if (namedType.AllInterfaces.Any(candidate =>
            candidate.Name == "IDictionary" &&
            candidate.ContainingNamespace?.ToDisplayString() == "System.Collections"))
        {
            return true;
        }

        return IsDictionaryGenericDefinition(namedType) ||
               namedType.AllInterfaces.Any(IsDictionaryGenericDefinition);
    }

    private static bool IsDictionaryGenericDefinition(INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return false;

        var definition = type.ConstructedFrom;
        return definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" &&
               definition.Name is "IDictionary" or "IReadOnlyDictionary";
    }

    internal static ITypeSymbol UnwrapNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
    }

    /// <summary>
    /// Classifies how the code generator must construct a value for a collection-shaped property or
    /// parameter so that the result is assignable to the declared type. This is the single
    /// construction matrix shared with the analyzer (mirrored by
    /// <c>Cvoya.Graph.Analyzers.AnalyzerHelper.IsConstructibleCollectionType</c>): only the shapes
    /// enumerated here are code-generable, and the analyzer rejects everything else via CG004/CG005.
    /// Arrays are limited to one dimension because LINQ materialization produces a vector.
    /// </summary>
    internal static CollectionConstructionKind GetCollectionConstructionKind(ITypeSymbol type)
    {
        // String enumerates chars but is never a collection property here.
        if (type.SpecialType == SpecialType.System_String)
            return CollectionConstructionKind.None;

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.Rank == 1
                ? CollectionConstructionKind.Array
                : CollectionConstructionKind.None;
        }

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

        // These types have no SpecialType. Match their metadata identity and require the runtime
        // assembly that supplies their IEnumerable<T> contract, preventing source-defined lookalikes
        // (including types with the same simple name but a different generic arity) from matching.
        if (IsRuntimeCollectionDefinition(definition, "List`1"))
            return CollectionConstructionKind.List;

        if (IsRuntimeCollectionDefinition(definition, "HashSet`1") ||
            IsRuntimeCollectionDefinition(definition, "ISet`1") ||
            IsRuntimeCollectionDefinition(definition, "IReadOnlySet`1"))
        {
            return CollectionConstructionKind.Set;
        }

        // Any other concrete/custom collection (Queue<T>, SortedSet<T>, ObservableCollection<T>, ...)
        // cannot be constructed by assigning a List<T>/HashSet<T>/array, so it is unsupported.
        return CollectionConstructionKind.None;
    }

    private static bool IsRuntimeCollectionDefinition(INamedTypeSymbol definition, string metadataName)
    {
        if (definition.MetadataName != metadataName ||
            definition.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic")
        {
            return false;
        }

        return IsFrameworkAssembly(definition.ContainingAssembly);
    }

    private static bool IsFrameworkAssembly(IAssemblySymbol assembly)
    {
        return assembly.Identity.Name switch
        {
            "System.Private.CoreLib" => HasPublicKeyToken(assembly, [0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e]),
            "System.Runtime" or "System.Collections" => HasPublicKeyToken(assembly, [0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a]),
            "mscorlib" or "System" => HasPublicKeyToken(assembly, [0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89]),
            "netstandard" => HasPublicKeyToken(assembly, [0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51]),
            _ when assembly.Identity.Name.StartsWith("System.", StringComparison.Ordinal) =>
                HasPublicKeyToken(assembly, [0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a]) ||
                HasPublicKeyToken(assembly, [0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e]),
            _ => false,
        };
    }

    private static bool HasPublicKeyToken(IAssemblySymbol assembly, byte[] expected)
    {
        return assembly.Identity.PublicKeyToken.SequenceEqual(expected);
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

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers;

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

    public static bool ImplementsINode(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.INode");
    }

    public static bool ImplementsIRelationship(INamedTypeSymbol type)
    {
        return ImplementsInterface(type, "Cvoya.Graph.IRelationship");
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, string interfaceName)
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

            if (namespaceName == "Cvoya.Graph" &&
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

        // Check if it's a collection of simple types. Only shapes the generator can construct
        // (arrays, List<T>/list interfaces, HashSet<T>/set interfaces) are valid; anything else
        // would pass analysis and then fail to compile in generated source.
        if (IsCollectionOfSimpleTypes(type))
            return IsConstructibleCollectionType(type);

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
                return result.IsValid && IsConstructibleCollectionType(type);
            }
        }

        return false;
    }

    private static bool IsUnsupportedFrameworkType(ITypeSymbol type)
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

        // Check if it's a collection of simple types the generator can construct (see
        // IsConstructibleCollectionType); non-constructible shapes would fail to compile downstream.
        if (IsCollectionOfSimpleTypes(type))
            return IsConstructibleCollectionType(type);

        return false;
    }

    public static bool IsSimpleType(ITypeSymbol type)
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

        // Check specific types by full name - matching Graph logic exactly
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
            "Cvoya.Graph.Point" => true,
            _ => false
        };
    }

    public bool IsComplexType(ITypeSymbol type)
    {
        type = UnwrapNullableValueType(type);

        // object itself (and collections/dictionaries of it) is never complex - it carries no
        // discoverable property shape to serialize.
        if (type.SpecialType == SpecialType.System_Object)
            return false;

        // A complex type is one that is not simple and not a collection
        if (IsSimpleType(type))
            return false;

        if (IsCollectionOfSimpleTypes(type))
            return false;

        if (IsCollectionOfComplexTypes(type))
            return false;

        // Dictionaries are never considered complex; see IsDictionaryType.
        if (IsDictionaryType(type))
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

    /// <summary>
    /// Whether a collection-shaped type is one the serialization source generator can construct so
    /// the deserialized value is assignable to the declared type: one-dimensional arrays, <c>List&lt;T&gt;</c> and
    /// list-compatible interfaces, and <c>HashSet&lt;T&gt;</c> and set-compatible interfaces.
    /// Concrete/custom collections (for example <c>Queue&lt;T&gt;</c>, <c>SortedSet&lt;T&gt;</c>,
    /// <c>ObservableCollection&lt;T&gt;</c>) are not constructible and are rejected by CG004/CG005 so
    /// the analyzer and generator agree. Mirrors
    /// <c>Cvoya.Graph.Serialization.CodeGen.GraphDataModel.GetCollectionConstructionKind</c>.
    /// </summary>
    public static bool IsConstructibleCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return false;

        if (type is IArrayTypeSymbol arrayType)
            return arrayType.Rank == 1;

        if (type is not INamedTypeSymbol { IsGenericType: true } namedType)
            return false;

        var definition = namedType.ConstructedFrom;

        // Interfaces a List<T> instance is assignable to.
        switch (definition.SpecialType)
        {
            case SpecialType.System_Collections_Generic_IEnumerable_T:
            case SpecialType.System_Collections_Generic_ICollection_T:
            case SpecialType.System_Collections_Generic_IList_T:
            case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
            case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                return true;
        }

        // Concrete List<T>/HashSet<T> and the set interfaces have no SpecialType. Match their
        // metadata identity and require them to come from the same runtime assembly as the
        // IEnumerable<T> they implement, so a source-defined lookalike cannot be misclassified.
        return IsRuntimeCollectionDefinition(definition, "List`1") ||
               IsRuntimeCollectionDefinition(definition, "HashSet`1") ||
               IsRuntimeCollectionDefinition(definition, "ISet`1") ||
               IsRuntimeCollectionDefinition(definition, "IReadOnlySet`1");
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
            "mscorlib" => HasPublicKeyToken(assembly, [0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89]),
            "netstandard" => HasPublicKeyToken(assembly, [0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51]),
            _ => false,
        };
    }

    private static bool HasPublicKeyToken(IAssemblySymbol assembly, byte[] expected)
    {
        return assembly.Identity.PublicKeyToken.SequenceEqual(expected);
    }

    /// <summary>
    /// Checks if a type is a dictionary shape: a concrete type implementing the non-generic
    /// <see cref="System.Collections.IDictionary"/>, or any type that is or implements
    /// <see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> or
    /// <see cref="System.Collections.Generic.IReadOnlyDictionary{TKey,TValue}"/>.
    /// Dictionaries are not supported as graph properties.
    /// </summary>
    public static bool IsDictionaryType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        if (namedType.AllInterfaces.Any(i => i.Name == "IDictionary" && i.ContainingNamespace?.ToDisplayString() == "System.Collections"))
            return true;

        if (IsDictionaryGenericDefinition(namedType))
            return true;

        return namedType.AllInterfaces.Any(IsDictionaryGenericDefinition);
    }

    private static bool IsDictionaryGenericDefinition(INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return false;

        var definition = type.ConstructedFrom;
        var name = definition.Name;
        var ns = definition.ContainingNamespace?.ToDisplayString();

        return ns == "System.Collections.Generic" && (name == "IDictionary" || name == "IReadOnlyDictionary");
    }

    public static bool IsCollectionOfSimpleTypes(ITypeSymbol type)
    {
        // Exclude string (even though it implements IEnumerable)
        if (type.SpecialType == SpecialType.System_String)
            return false;

        // Dictionaries are never a simple collection shape, even though they implement IEnumerable.
        if (IsDictionaryType(type))
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

        // Dictionaries are never a supported collection shape, even though they implement IEnumerable.
        if (IsDictionaryType(type))
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

    private static bool ImplementsIEnumerable(ITypeSymbol type)
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

    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
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
        type = UnwrapNullableValueType(type);

        if (type is not INamedTypeSymbol namedType)
            return new ComplexTypeValidationResult(false, "Type is not a named type", false);

        // Check all properties recursively
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return ValidateComplexTypeRecursive(namedType, visited);
    }

    private ComplexTypeValidationResult ValidateComplexTypeRecursive(INamedTypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        type = (INamedTypeSymbol)UnwrapNullableValueType(type);

        if (visited.Contains(type))
            return new ComplexTypeValidationResult(true, null, false); // Avoid infinite recursion

        visited.Add(type);

        if (type.TypeKind == TypeKind.Struct && !CanDeserializeComplexStruct(type))
        {
            return new ComplexTypeValidationResult(
                false,
                $"Struct {type.Name} has serialized properties that cannot be reconstructed",
                false);
        }

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod is not null &&
                !property.IsStatic &&
                !SerializationShouldIgnoreProperty(property));
        foreach (var property in properties)
        {
            // Check if property is a graph interface type
            if (IsGraphInterfaceType(property.Type))
            {
                return new ComplexTypeValidationResult(
                    false,
                    $"Property {property.Name} is a graph interface type",
                    true);
            }

            // Check collections recursively
            if (IsCollectionType(property.Type))
            {
                if (!IsConstructibleCollectionType(property.Type))
                {
                    return new ComplexTypeValidationResult(
                        false,
                        $"Property {property.Name} uses an unsupported collection declaration",
                        false);
                }

                var elementType = GetCollectionElementType(property.Type);
                if (elementType != null && IsGraphInterfaceType(elementType))
                {
                    return new ComplexTypeValidationResult(
                        false,
                        $"Property {property.Name} is a collection of graph interface types",
                        true);
                }
            }

            // Check nested complex types recursively
            if (IsComplexType(property.Type))
            {
                var complexType = UnwrapNullableValueType(property.Type) as INamedTypeSymbol;
                if (complexType is null)
                {
                    return new ComplexTypeValidationResult(
                        false,
                        $"Property {property.Name} is not a named type",
                        false);
                }

                var result = ValidateComplexTypeRecursive(complexType, visited);
                if (!result.IsValid)
                {
                    return result;
                }
            }
            else if (IsCollectionOfComplexTypes(property.Type))
            {
                var elementType = GetCollectionElementType(property.Type);
                if (elementType is not null &&
                    UnwrapNullableValueType(elementType) is INamedTypeSymbol namedElementType)
                {
                    var result = ValidateComplexTypeRecursive(namedElementType, visited);
                    if (!result.IsValid)
                    {
                        return result;
                    }
                }
            }
        }

        return new ComplexTypeValidationResult(true, null, false);
    }

    private static bool CanDeserializeComplexStruct(INamedTypeSymbol type)
    {
        if (type.GetMembers().OfType<IFieldSymbol>().Any(field => field.IsRequired))
            return false;

        var constructorOnlyProperties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod is not null &&
                !property.IsStatic &&
                !SerializationShouldIgnoreProperty(property) &&
                property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
            .ToList();

        if (constructorOnlyProperties.Count == 0)
            return true;

        return type.InstanceConstructors
            .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .Any(constructor => constructorOnlyProperties.All(property =>
                constructor.Parameters.Any(parameter =>
                    string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase) &&
                    SymbolEqualityComparer.Default.Equals(parameter.Type, property.Type))));
    }

    private static bool SerializationShouldIgnoreProperty(IPropertySymbol property)
    {
        var attribute = property.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass?.Name == "PropertyAttribute" &&
            candidate.AttributeClass.ContainingNamespace?.ToDisplayString() == "Cvoya.Graph");

        return attribute?.NamedArguments.Any(argument =>
            argument.Key == "Ignore" && argument.Value.Value is true) == true;
    }

    private static ITypeSymbol UnwrapNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } namedType &&
               namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
    }

    public static bool IsNullableType(ITypeSymbol type)
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
    public bool ContainsGraphInterface { get; }

    public ComplexTypeValidationResult(bool isValid, string? errorMessage, bool containsGraphInterface)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        ContainsGraphInterface = containsGraphInterface;
    }
}

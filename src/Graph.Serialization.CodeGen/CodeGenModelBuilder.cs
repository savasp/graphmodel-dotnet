// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;

using Microsoft.CodeAnalysis;


internal sealed class CodeGenModelBuilder
{
    private readonly Dictionary<INamedTypeSymbol, SerializableTypeModel> models = new(SymbolEqualityComparer.Default);
    private readonly List<SerializableTypeModel> orderedModels = [];
    private readonly GraphAttributeSymbols graphAttributes;
    private bool hasUnsupportedTypeShape;

    private CodeGenModelBuilder(GraphAttributeSymbols graphAttributes)
    {
        this.graphAttributes = graphAttributes;
    }

    public static TypeDiscoverySet Build(INamedTypeSymbol root)
    {
        var builder = new CodeGenModelBuilder(GraphAttributeSymbols.Resolve(root.ContainingAssembly));
        var rootModel = builder.BuildType(root);

        if (builder.hasUnsupportedTypeShape)
        {
            return TypeDiscoverySet.Empty;
        }

        return new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([rootModel]),
            EquatableArray<SerializableTypeModel>.From(builder.orderedModels));
    }

    public static TypeDiscoverySet Build(IEnumerable<INamedTypeSymbol> roots)
    {
        var result = TypeDiscoverySet.Empty;
        foreach (var root in roots)
        {
            result = TypeDiscoverySet.Merge(result, Build(root));
        }

        return result;
    }

    private SerializableTypeModel BuildType(INamedTypeSymbol type)
    {
        if (models.TryGetValue(type, out var model))
        {
            return model;
        }

        var serializationProperties = Utils.GetAllProperties(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type, graphAttributes))
            .Select(BuildProperty)
            .ToList();

        var deserializationProperties = GetAllPropertiesIncludingInterfacesForDeserialization(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type, graphAttributes))
            .Select(BuildProperty)
            .ToList();

        var schemaProperties = GetAllPropertiesIncludingInterfacesForSchema(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type, graphAttributes))
            .Select(BuildProperty)
            .ToList();

        var constructors = type.Constructors
            .Where(constructor => !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(constructor => constructor.Parameters.Length)
            .ToList();

        var constructorOnlyProperties = deserializationProperties
            .Where(property => !property.SetterDeclaredPublic || property.SetterIsInitOnly)
            .ToList();

        var needsConstructor = constructorOnlyProperties.Count > 0 ||
            deserializationProperties.Any(property => property.IsRequired) ||
            type.IsRecord;
        var selectedConstructor = constructors.Count == 0
            ? null
            : FindBestConstructor(constructors, deserializationProperties) ?? constructors[0];
        var constructor = selectedConstructor is null
            ? null
            : BuildConstructor(selectedConstructor, deserializationProperties);

        if (type.TypeKind == TypeKind.Struct &&
            GetKind(type) == SerializableTypeKind.Complex &&
            !CanDeserializeComplexStruct(type, deserializationProperties, selectedConstructor))
        {
            hasUnsupportedTypeShape = true;
        }

        var complexPropertyTypes = DiscoverComplexPropertyTypes(type).ToList();

        model = new SerializableTypeModel(
            BuildTypeReference(type),
            type.Name,
            Utils.GetNamespaceName(type),
            Utils.GetUniqueSerializerClassName(type),
            GetUniqueHintName(type),
            Utils.GetLabelFromType(type, graphAttributes),
            GetKind(type),
            type.IsRecord,
            constructors.Any(constructor => constructor.Parameters.Length == 0),
            needsConstructor,
            constructor,
            EquatableArray<SerializablePropertyModel>.From(serializationProperties),
            EquatableArray<SerializablePropertyModel>.From(deserializationProperties),
            EquatableArray<SerializablePropertyModel>.From(schemaProperties),
            EquatableArray<string>.From(GetBaseTypeIdentities(type)),
            EquatableArray<string>.From(complexPropertyTypes.Select(complexType => GetTypeIdentity(complexType))));

        models[type] = model;
        orderedModels.Add(model);

        foreach (var complexPropertyType in complexPropertyTypes)
        {
            BuildType(complexPropertyType);
        }

        return model;
    }

    private SerializablePropertyModel BuildProperty(IPropertySymbol property)
    {
        return new SerializablePropertyModel(
            property.Name,
            Utils.GetPropertyName(property, graphAttributes),
            property.ContainingType.ToDisplayString(),
            property.ContainingType.TypeKind == TypeKind.Interface,
            BuildTypeReference(property.Type),
            property.SetMethod is not null,
            property.SetMethod?.IsInitOnly == true,
            property.SetMethod?.DeclaredAccessibility == Accessibility.Public,
            property.IsRequired,
            HidesOrOverridesBaseProperty(property));
    }

    private static bool HidesOrOverridesBaseProperty(IPropertySymbol property)
    {
        for (var baseType = property.ContainingType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.GetMembers(property.Name)
                .OfType<IPropertySymbol>()
                .Any(candidate => !candidate.IsStatic && candidate.DeclaredAccessibility == Accessibility.Public))
            {
                return true;
            }
        }

        return false;
    }

    private TypeReferenceModel BuildTypeReference(ITypeSymbol type)
    {
        var elementType = GraphDataModel.GetCollectionElementType(type);
        var namedType = type as INamedTypeSymbol;
        var isSimple = GraphDataModel.IsSimple(type);
        var isCollectionOfSimple = GraphDataModel.IsCollectionOfSimple(type);
        var isCollectionOfComplex = GraphDataModel.IsCollectionOfComplex(type);
        var collectionConstructionKind = GraphDataModel.GetCollectionConstructionKind(type);
        var serializerNamedType = !isSimple && UnwrapNullableValueType(type) is INamedTypeSymbol underlyingType
            ? underlyingType
            : namedType;

        // Element shapes are covered by the recursive BuildTypeReference(elementType) call below,
        // which re-applies this same check at every nesting level.
        if (GraphDataModel.IsUnsupportedFrameworkType(type) || GraphDataModel.IsDictionaryType(type))
        {
            hasUnsupportedTypeShape = true;
        }

        if ((isCollectionOfSimple || isCollectionOfComplex) &&
            collectionConstructionKind == CollectionConstructionKind.None)
        {
            hasUnsupportedTypeShape = true;
        }

        return new TypeReferenceModel(
            GetTypeIdentity(type),
            type.ToDisplayString(),
            Utils.GetTypeOfName(type),
            type.Name,
            serializerNamedType is null ? string.Empty : Utils.GetNamespaceName(serializerNamedType),
            serializerNamedType is null ? string.Empty : Utils.GetUniqueSerializerClassName(serializerNamedType),
            type.IsReferenceType,
            type.IsValueType,
            IsNullableType(type),
            type.NullableAnnotation == NullableAnnotation.Annotated || (type.CanBeReferencedByName && !type.IsValueType),
            type.TypeKind == TypeKind.Array,
            isSimple,
            isCollectionOfSimple,
            isCollectionOfComplex,
            type.TypeKind == TypeKind.Enum,
            type.TypeKind,
            type.SpecialType,
            collectionConstructionKind,
            elementType is null ? null : BuildTypeReference(elementType));
    }

    private ConstructorModel BuildConstructor(
        IMethodSymbol constructor,
        IReadOnlyCollection<SerializablePropertyModel> properties)
    {
        return new ConstructorModel(EquatableArray<ParameterModel>.From(
            constructor.Parameters.Select(parameter => new ParameterModel(
                parameter.Name,
                BuildTypeReference(parameter.Type),
                FindPropertyLabelForParameter(parameter, properties),
                parameter.HasExplicitDefaultValue,
                parameter.HasExplicitDefaultValue
                    ? DefaultValueFormatter.Format(parameter.ExplicitDefaultValue, parameter.Type)
                    : string.Empty))));
    }

    private static List<IPropertySymbol> GetAllPropertiesIncludingInterfacesForDeserialization(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var seenProperties = new HashSet<string>();

        for (var currentType = type; currentType != null; currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public &&
                    property.GetMethod != null &&
                    !property.IsStatic &&
                    !property.IsIndexer &&
                    seenProperties.Add(property.Name)))
            {
                properties.Add(property);
            }
        }

        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var property in interfaceType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => property.DeclaredAccessibility == Accessibility.Public &&
                    property.GetMethod != null &&
                    !property.IsIndexer &&
                    seenProperties.Add(property.Name)))
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    private static List<IPropertySymbol> GetAllPropertiesIncludingInterfacesForSchema(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var interfaceType in type.AllInterfaces)
        {
            foreach (var property in interfaceType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => ShouldIncludeSchemaProperty(property) && seenProperties.Add(property.Name)))
            {
                properties.Add(property);
            }
        }

        for (var currentType = type; currentType != null; currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => ShouldIncludeSchemaProperty(property) && seenProperties.Add(property.Name)))
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    private static bool ShouldIncludeSchemaProperty(IPropertySymbol property)
    {
        if (property.DeclaredAccessibility != Accessibility.Public)
            return false;

        if (property.IsIndexer)
            return false;

        if (property.IsStatic)
            return false;

        if (property.GetMethod == null)
            return false;

        if (property.ContainingType.TypeKind == TypeKind.Interface)
        {
            return GraphDataModel.IsSimple(property.Type) ||
                   GraphDataModel.IsCollectionOfSimple(property.Type);
        }

        if (property.SetMethod == null)
            return false;

        return GraphDataModel.IsSimple(property.Type) ||
               GraphDataModel.IsCollectionOfSimple(property.Type) ||
               !GraphDataModel.IsSimple(property.Type) ||
               GraphDataModel.IsCollectionOfComplex(property.Type);
    }

    private IEnumerable<INamedTypeSymbol> DiscoverComplexPropertyTypes(INamedTypeSymbol type)
    {
        // Discover dependency serializers from the same effective property set the entity serializer
        // is generated over - inherited public instance properties and applicable interface
        // properties - rather than only the members declared directly on the concrete type. A
        // concrete node/relationship can inherit a complex property from an abstract base (which is
        // never itself a generator root), and that property still participates in the generated
        // serializer; without walking the base/interface set here its complex value type would be
        // absent from the dependency catalog and the generated code would reference an unregistered
        // serializer (#371). Reusing the deserialization property walk preserves override/hiding
        // precedence (most-derived declaration wins); dependency identities are deduplicated so
        // discovery stays deterministic and emits no duplicate serializers.
        var seenComplexTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in GetAllPropertiesIncludingInterfacesForDeserialization(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type, graphAttributes)))
        {
            if (GetComplexPropertyType(property.Type) is INamedTypeSymbol complexType &&
                seenComplexTypes.Add(GetTypeIdentity(complexType)))
            {
                yield return complexType;
            }
        }
    }

    private static INamedTypeSymbol? GetComplexPropertyType(ITypeSymbol propertyType)
    {
        if (GraphDataModel.IsSimple(propertyType) || GraphDataModel.IsCollectionOfSimple(propertyType))
        {
            return null;
        }

        if (GraphDataModel.IsCollectionOfComplex(propertyType))
        {
            var elementType = GraphDataModel.GetCollectionElementType(propertyType);
            var unwrappedElementType = elementType is null ? null : UnwrapNullableValueType(elementType);
            return unwrappedElementType is INamedTypeSymbol namedElementType && IsSerializableComplexType(namedElementType)
                ? namedElementType
                : null;
        }

        var unwrappedPropertyType = UnwrapNullableValueType(propertyType);
        return unwrappedPropertyType is INamedTypeSymbol namedPropertyType && IsSerializableComplexType(namedPropertyType)
            ? namedPropertyType
            : null;
    }

    private static bool IsSerializableComplexType(INamedTypeSymbol type)
    {
        if (type.IsAbstract)
        {
            return false;
        }

        // Structs are always constructible via `new T()` (the C#-guaranteed parameterless
        // constructor), so a struct complex-property value is discoverable and gets its own
        // generated serializer - closing the gap where the analyzer accepts a nested struct but no
        // serializer was ever emitted for it. INode/IRelationship structs are rejected separately by
        // CG014 and never reach here as an entity root.
        if (type.TypeKind == TypeKind.Struct)
        {
            return true;
        }

        return type.TypeKind == TypeKind.Class &&
               type.InstanceConstructors.Any(constructor =>
                   constructor.DeclaredAccessibility == Accessibility.Public &&
                   constructor.Parameters.Length == 0);
    }

    private static IMethodSymbol? FindBestConstructor(
        List<IMethodSymbol> constructors,
        List<SerializablePropertyModel> allProperties)
    {
        var constructorScores = constructors.Select(constructor =>
        {
            var matchingProperties = constructor.Parameters
                .Select(parameter => allProperties.FirstOrDefault(property =>
                    string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                    property.Type.Identity == GetTypeIdentity(parameter.Type)))
                .Where(property => property != null)
                .ToList();

            return new
            {
                Constructor = constructor,
                ConstructorOnlyMatches = matchingProperties.Count(property =>
                    property is not null && !property.SetterDeclaredPublic),
                TotalMatches = matchingProperties.Count,
                ExtraParams = constructor.Parameters.Length - matchingProperties.Count,
            };
        }).ToList();

        return constructorScores
            .OrderByDescending(score => score.ConstructorOnlyMatches)
            .ThenByDescending(score => score.TotalMatches)
            .ThenBy(score => score.ExtraParams)
            .Select(score => score.Constructor)
            .FirstOrDefault();
    }

    private static bool CanDeserializeComplexStruct(
        INamedTypeSymbol type,
        IReadOnlyCollection<SerializablePropertyModel> properties,
        IMethodSymbol? constructor)
    {
        if (type.GetMembers().OfType<IFieldSymbol>().Any(field => field.IsRequired))
            return false;

        return properties.All(property =>
            property.SetterDeclaredPublic ||
            constructor?.Parameters.Any(parameter =>
                string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase) &&
                property.Type.Identity == GetTypeIdentity(parameter.Type)) == true);
    }

    private static ITypeSymbol UnwrapNullableValueType(ITypeSymbol type) =>
        GraphDataModel.UnwrapNullableValueType(type);

    private static IEnumerable<string> GetBaseTypeIdentities(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return GetTypeIdentity(current);
        }
    }

    private static SerializableTypeKind GetKind(INamedTypeSymbol type)
    {
        if (ImplementsGraphInterface(type, "INode"))
        {
            return SerializableTypeKind.Node;
        }

        if (ImplementsGraphInterface(type, "IRelationship"))
        {
            return SerializableTypeKind.Relationship;
        }

        return SerializableTypeKind.Complex;
    }

    private static bool ImplementsGraphInterface(INamedTypeSymbol type, string interfaceName)
    {
        return type.AllInterfaces.Any(candidate =>
            candidate.Name == interfaceName &&
            candidate.ContainingNamespace?.ToString() == "Cvoya.Graph");
    }

    private static string FindPropertyNameForParameter(IParameterSymbol parameter)
    {
        var parameterName = parameter.Name;

        return Utils.GetPropertyNameFromParameter(parameter);
    }

    private static string FindPropertyLabelForParameter(
        IParameterSymbol parameter,
        IReadOnlyCollection<SerializablePropertyModel> properties)
    {
        var propertyName = FindPropertyNameForParameter(parameter);
        return properties.FirstOrDefault(property =>
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            ?.Label ?? propertyName;
    }

    private static string GetUniqueHintName(INamedTypeSymbol type)
    {
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(" ", "")
            .Replace("?", "_Nullable")
            .Replace("+", "_");

        if (Utils.RequiresGenericIdentitySuffix(type))
        {
            fullName += $"_{Utils.GetStableTypeIdentitySuffix(type)}";
        }

        return $"{fullName}Serializer.g.cs";
    }

    private static string GetTypeIdentity(ITypeSymbol type)
    {
        var identityType = type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;

        return identityType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        if (type is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            return true;

        return false;
    }
}

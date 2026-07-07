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

namespace Cvoya.Graph.Model.Serialization.CodeGen;

using Microsoft.CodeAnalysis;


internal sealed class CodeGenModelBuilder
{
    private readonly Dictionary<INamedTypeSymbol, SerializableTypeModel> models = new(SymbolEqualityComparer.Default);
    private readonly List<SerializableTypeModel> orderedModels = [];

    private CodeGenModelBuilder()
    {
    }

    public static TypeDiscoverySet Build(INamedTypeSymbol root)
    {
        var builder = new CodeGenModelBuilder();
        var rootModel = builder.BuildType(root);

        return new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([rootModel]),
            EquatableArray<SerializableTypeModel>.From(builder.orderedModels));
    }

    public static TypeDiscoverySet Build(IEnumerable<INamedTypeSymbol> roots)
    {
        var builder = new CodeGenModelBuilder();
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        var rootModels = roots
            .Select(builder.BuildType)
            .Where(rootModel => seenRoots.Add(rootModel.Type.Identity))
            .ToList();

        return new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From(rootModels),
            EquatableArray<SerializableTypeModel>.From(builder.orderedModels));
    }

    private SerializableTypeModel BuildType(INamedTypeSymbol type)
    {
        if (models.TryGetValue(type, out var model))
        {
            return model;
        }

        var serializationProperties = Utils.GetAllProperties(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type))
            .Select(BuildProperty)
            .ToList();

        var deserializationProperties = GetAllPropertiesIncludingInterfacesForDeserialization(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type))
            .Select(BuildProperty)
            .ToList();

        var schemaProperties = GetAllPropertiesIncludingInterfacesForSchema(type)
            .Where(property => !Utils.SerializationShouldSkipProperty(property, type))
            .Select(BuildProperty)
            .ToList();

        var constructors = type.Constructors
            .Where(constructor => !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(constructor => constructor.Parameters.Length)
            .ToList();

        var constructorOnlyProperties = deserializationProperties
            .Where(property => !property.HasSetter || property.SetterIsInitOnly)
            .ToList();

        var needsConstructor = constructorOnlyProperties.Count > 0 || type.IsRecord;
        var constructor = constructors.Count == 0
            ? null
            : BuildConstructor(FindBestConstructor(constructors, deserializationProperties) ?? constructors[0]);

        var complexPropertyTypes = DiscoverComplexPropertyTypes(type).ToList();

        model = new SerializableTypeModel(
            BuildTypeReference(type),
            type.Name,
            Utils.GetNamespaceName(type),
            Utils.GetUniqueSerializerClassName(type),
            GetUniqueHintName(type),
            Utils.GetLabelFromType(type),
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

    private static SerializablePropertyModel BuildProperty(IPropertySymbol property)
    {
        return new SerializablePropertyModel(
            property.Name,
            Utils.GetPropertyName(property),
            property.ContainingType.ToDisplayString(),
            property.ContainingType.TypeKind == TypeKind.Interface,
            BuildTypeReference(property.Type),
            property.SetMethod is not null,
            property.SetMethod?.IsInitOnly == true,
            property.SetMethod?.DeclaredAccessibility == Accessibility.Public);
    }

    private static TypeReferenceModel BuildTypeReference(ITypeSymbol type)
    {
        var elementType = GraphDataModel.GetCollectionElementType(type);
        var namedType = type as INamedTypeSymbol;

        return new TypeReferenceModel(
            GetTypeIdentity(type),
            type.ToDisplayString(),
            Utils.GetTypeOfName(type),
            type.Name,
            namedType is null ? string.Empty : Utils.GetNamespaceName(namedType),
            namedType is null ? string.Empty : Utils.GetUniqueSerializerClassName(namedType),
            type.IsReferenceType,
            type.IsValueType,
            IsNullableType(type),
            type.NullableAnnotation == NullableAnnotation.Annotated || (type.CanBeReferencedByName && !type.IsValueType),
            type.TypeKind == TypeKind.Array,
            GraphDataModel.IsSimple(type),
            GraphDataModel.IsCollectionOfSimple(type),
            GraphDataModel.IsCollectionOfComplex(type),
            type.TypeKind == TypeKind.Enum,
            type.TypeKind,
            type.SpecialType,
            elementType is null ? null : BuildTypeReference(elementType));
    }

    private static ConstructorModel BuildConstructor(IMethodSymbol constructor)
    {
        return new ConstructorModel(EquatableArray<ParameterModel>.From(
            constructor.Parameters.Select(parameter => new ParameterModel(
                parameter.Name,
                BuildTypeReference(parameter.Type),
                FindPropertyNameForParameter(parameter),
                parameter.HasExplicitDefaultValue,
                parameter.HasExplicitDefaultValue
                    ? FormatDefaultValue(parameter.ExplicitDefaultValue, parameter.Type)
                    : string.Empty))));
    }

    private static IEnumerable<IPropertySymbol> GetAllPropertiesIncludingInterfacesForDeserialization(INamedTypeSymbol type)
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
                    seenProperties.Add(property.Name)))
            {
                properties.Add(property);
            }
        }

        return properties;
    }

    private static IEnumerable<IPropertySymbol> GetAllPropertiesIncludingInterfacesForSchema(INamedTypeSymbol type)
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

    private static IEnumerable<INamedTypeSymbol> DiscoverComplexPropertyTypes(INamedTypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.GetMethod != null &&
                property.SetMethod != null)
            .Select(property => GetComplexPropertyType(property.Type))
            .OfType<INamedTypeSymbol>();
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
            return elementType is INamedTypeSymbol namedElementType && IsSerializableComplexType(namedElementType)
                ? namedElementType
                : null;
        }

        return propertyType is INamedTypeSymbol namedPropertyType && IsSerializableComplexType(namedPropertyType)
            ? namedPropertyType
            : null;
    }

    private static bool IsSerializableComplexType(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class &&
               !type.IsAbstract &&
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
                    string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(property => property != null)
                .ToList();

            return new
            {
                Constructor = constructor,
                TotalMatches = matchingProperties.Count,
                ExtraParams = constructor.Parameters.Length - matchingProperties.Count,
            };
        }).ToList();

        return constructorScores
            .OrderByDescending(score => score.TotalMatches)
            .ThenBy(score => score.ExtraParams)
            .Select(score => score.Constructor)
            .FirstOrDefault();
    }

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
            candidate.ContainingNamespace?.ToString() == "Cvoya.Graph.Model");
    }

    private static string FindPropertyNameForParameter(IParameterSymbol parameter)
    {
        var parameterName = parameter.Name;

        return parameterName.ToLowerInvariant() switch
        {
            "startnodeid" => "StartNodeId",
            "endnodeid" => "EndNodeId",
            "id" => "Id",
            "direction" => "Direction",
            _ => Utils.GetPropertyNameFromParameter(parameter)
        };
    }

    private static string FormatDefaultValue(object? defaultValue, ITypeSymbol type)
    {
        return defaultValue switch
        {
            null when type.IsReferenceType => "null",
            null => $"default({type.ToDisplayString()})",
            string str => $"\"{str}\"",
            bool b => b.ToString().ToLowerInvariant(),
            _ => defaultValue.ToString() ?? "null"
        };
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

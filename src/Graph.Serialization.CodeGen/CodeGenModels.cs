// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Serialization.CodeGen;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;


internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
{
    private readonly ImmutableArray<T> items;

    public EquatableArray(ImmutableArray<T> items)
    {
        this.items = items.IsDefault ? ImmutableArray<T>.Empty : items;
    }

    public ImmutableArray<T> Items => items.IsDefault ? ImmutableArray<T>.Empty : items;

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public static EquatableArray<T> From(IEnumerable<T> items)
    {
        return new EquatableArray<T>(items.ToImmutableArray());
    }

    public bool Equals(EquatableArray<T> other)
    {
        return Items.SequenceEqual(other.Items, EqualityComparer<T>.Default);
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in Items)
            {
                hash = (hash * 31) + EqualityComparer<T>.Default.GetHashCode(item!);
            }

            return hash;
        }
    }
}

internal enum SerializableTypeKind
{
    Complex,
    Node,
    Relationship,
}

internal sealed class TypeReferenceModel : IEquatable<TypeReferenceModel>
{
    public TypeReferenceModel(
        string identity,
        string displayName,
        string typeOfName,
        string name,
        string generatedNamespaceName,
        string serializerClassName,
        bool isReferenceType,
        bool isValueType,
        bool isNullable,
        bool isSchemaNullable,
        bool isArray,
        bool isSimple,
        bool isCollectionOfSimple,
        bool isCollectionOfComplex,
        bool isEnum,
        TypeKind typeKind,
        SpecialType specialType,
        TypeReferenceModel? elementType)
    {
        Identity = identity;
        DisplayName = displayName;
        TypeOfName = typeOfName;
        Name = name;
        GeneratedNamespaceName = generatedNamespaceName;
        SerializerClassName = serializerClassName;
        IsReferenceType = isReferenceType;
        IsValueType = isValueType;
        IsNullable = isNullable;
        IsSchemaNullable = isSchemaNullable;
        IsArray = isArray;
        IsSimple = isSimple;
        IsCollectionOfSimple = isCollectionOfSimple;
        IsCollectionOfComplex = isCollectionOfComplex;
        IsEnum = isEnum;
        TypeKind = typeKind;
        SpecialType = specialType;
        ElementType = elementType;
    }

    public string Identity { get; }

    public string DisplayName { get; }

    public string TypeOfName { get; }

    public string Name { get; }

    public string GeneratedNamespaceName { get; }

    public string SerializerClassName { get; }

    public bool IsReferenceType { get; }

    public bool IsValueType { get; }

    public bool IsNullable { get; }

    public bool IsSchemaNullable { get; }

    public bool IsArray { get; }

    public bool IsSimple { get; }

    public bool IsCollectionOfSimple { get; }

    public bool IsCollectionOfComplex { get; }

    public bool IsEnum { get; }

    public TypeKind TypeKind { get; }

    public SpecialType SpecialType { get; }

    public TypeReferenceModel? ElementType { get; }

    public bool Equals(TypeReferenceModel? other)
    {
        return other is not null &&
            Identity == other.Identity &&
            DisplayName == other.DisplayName &&
            TypeOfName == other.TypeOfName &&
            Name == other.Name &&
            GeneratedNamespaceName == other.GeneratedNamespaceName &&
            SerializerClassName == other.SerializerClassName &&
            IsReferenceType == other.IsReferenceType &&
            IsValueType == other.IsValueType &&
            IsNullable == other.IsNullable &&
            IsSchemaNullable == other.IsSchemaNullable &&
            IsArray == other.IsArray &&
            IsSimple == other.IsSimple &&
            IsCollectionOfSimple == other.IsCollectionOfSimple &&
            IsCollectionOfComplex == other.IsCollectionOfComplex &&
            IsEnum == other.IsEnum &&
            TypeKind == other.TypeKind &&
            SpecialType == other.SpecialType &&
            EqualityComparer<TypeReferenceModel?>.Default.Equals(ElementType, other.ElementType);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TypeReferenceModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Identity.GetHashCode();
            hash = (hash * 31) + DisplayName.GetHashCode();
            hash = (hash * 31) + TypeOfName.GetHashCode();
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + GeneratedNamespaceName.GetHashCode();
            hash = (hash * 31) + SerializerClassName.GetHashCode();
            hash = (hash * 31) + IsReferenceType.GetHashCode();
            hash = (hash * 31) + IsValueType.GetHashCode();
            hash = (hash * 31) + IsNullable.GetHashCode();
            hash = (hash * 31) + IsSchemaNullable.GetHashCode();
            hash = (hash * 31) + IsArray.GetHashCode();
            hash = (hash * 31) + IsSimple.GetHashCode();
            hash = (hash * 31) + IsCollectionOfSimple.GetHashCode();
            hash = (hash * 31) + IsCollectionOfComplex.GetHashCode();
            hash = (hash * 31) + IsEnum.GetHashCode();
            hash = (hash * 31) + TypeKind.GetHashCode();
            hash = (hash * 31) + SpecialType.GetHashCode();
            hash = (hash * 31) + (ElementType?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal sealed class SerializablePropertyModel : IEquatable<SerializablePropertyModel>
{
    public SerializablePropertyModel(
        string name,
        string label,
        string containingTypeDisplayName,
        bool containingTypeIsInterface,
        TypeReferenceModel type,
        bool hasSetter,
        bool setterIsInitOnly,
        bool setterDeclaredPublic)
    {
        Name = name;
        Label = label;
        ContainingTypeDisplayName = containingTypeDisplayName;
        ContainingTypeIsInterface = containingTypeIsInterface;
        Type = type;
        HasSetter = hasSetter;
        SetterIsInitOnly = setterIsInitOnly;
        SetterDeclaredPublic = setterDeclaredPublic;
    }

    public string Name { get; }

    public string Label { get; }

    public string ContainingTypeDisplayName { get; }

    public bool ContainingTypeIsInterface { get; }

    public TypeReferenceModel Type { get; }

    public bool HasSetter { get; }

    public bool SetterIsInitOnly { get; }

    public bool SetterDeclaredPublic { get; }

    public bool Equals(SerializablePropertyModel? other)
    {
        return other is not null &&
            Name == other.Name &&
            Label == other.Label &&
            ContainingTypeDisplayName == other.ContainingTypeDisplayName &&
            ContainingTypeIsInterface == other.ContainingTypeIsInterface &&
            Type.Equals(other.Type) &&
            HasSetter == other.HasSetter &&
            SetterIsInitOnly == other.SetterIsInitOnly &&
            SetterDeclaredPublic == other.SetterDeclaredPublic;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SerializablePropertyModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + Label.GetHashCode();
            hash = (hash * 31) + ContainingTypeDisplayName.GetHashCode();
            hash = (hash * 31) + ContainingTypeIsInterface.GetHashCode();
            hash = (hash * 31) + Type.GetHashCode();
            hash = (hash * 31) + HasSetter.GetHashCode();
            hash = (hash * 31) + SetterIsInitOnly.GetHashCode();
            hash = (hash * 31) + SetterDeclaredPublic.GetHashCode();
            return hash;
        }
    }
}

internal sealed class ParameterModel : IEquatable<ParameterModel>
{
    public ParameterModel(
        string name,
        TypeReferenceModel type,
        string propertyName,
        bool hasExplicitDefaultValue,
        string explicitDefaultValueExpression)
    {
        Name = name;
        Type = type;
        PropertyName = propertyName;
        HasExplicitDefaultValue = hasExplicitDefaultValue;
        ExplicitDefaultValueExpression = explicitDefaultValueExpression;
    }

    public string Name { get; }

    public TypeReferenceModel Type { get; }

    public string PropertyName { get; }

    public bool HasExplicitDefaultValue { get; }

    public string ExplicitDefaultValueExpression { get; }

    public bool Equals(ParameterModel? other)
    {
        return other is not null &&
            Name == other.Name &&
            Type.Equals(other.Type) &&
            PropertyName == other.PropertyName &&
            HasExplicitDefaultValue == other.HasExplicitDefaultValue &&
            ExplicitDefaultValueExpression == other.ExplicitDefaultValueExpression;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ParameterModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + Type.GetHashCode();
            hash = (hash * 31) + PropertyName.GetHashCode();
            hash = (hash * 31) + HasExplicitDefaultValue.GetHashCode();
            hash = (hash * 31) + ExplicitDefaultValueExpression.GetHashCode();
            return hash;
        }
    }
}

internal sealed class ConstructorModel : IEquatable<ConstructorModel>
{
    public ConstructorModel(EquatableArray<ParameterModel> parameters)
    {
        Parameters = parameters;
    }

    public EquatableArray<ParameterModel> Parameters { get; }

    public bool Equals(ConstructorModel? other)
    {
        return other is not null && Parameters.Equals(other.Parameters);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ConstructorModel);
    }

    public override int GetHashCode()
    {
        return Parameters.GetHashCode();
    }
}

internal sealed class SerializableTypeModel : IEquatable<SerializableTypeModel>
{
    public SerializableTypeModel(
        TypeReferenceModel type,
        string name,
        string namespaceName,
        string serializerClassName,
        string hintName,
        string label,
        SerializableTypeKind kind,
        bool isRecord,
        bool hasParameterlessPublicConstructor,
        bool needsConstructor,
        ConstructorModel? constructor,
        EquatableArray<SerializablePropertyModel> serializationProperties,
        EquatableArray<SerializablePropertyModel> deserializationProperties,
        EquatableArray<SerializablePropertyModel> schemaProperties,
        EquatableArray<string> baseTypeIdentities,
        EquatableArray<string> complexPropertyTypeIdentities)
    {
        Type = type;
        Name = name;
        NamespaceName = namespaceName;
        SerializerClassName = serializerClassName;
        HintName = hintName;
        Label = label;
        Kind = kind;
        IsRecord = isRecord;
        HasParameterlessPublicConstructor = hasParameterlessPublicConstructor;
        NeedsConstructor = needsConstructor;
        Constructor = constructor;
        SerializationProperties = serializationProperties;
        DeserializationProperties = deserializationProperties;
        SchemaProperties = schemaProperties;
        BaseTypeIdentities = baseTypeIdentities;
        ComplexPropertyTypeIdentities = complexPropertyTypeIdentities;
    }

    public TypeReferenceModel Type { get; }

    public string Name { get; }

    public string NamespaceName { get; }

    public string SerializerClassName { get; }

    public string HintName { get; }

    public string Label { get; }

    public SerializableTypeKind Kind { get; }

    public bool IsRecord { get; }

    public bool HasParameterlessPublicConstructor { get; }

    public bool NeedsConstructor { get; }

    public ConstructorModel? Constructor { get; }

    public EquatableArray<SerializablePropertyModel> SerializationProperties { get; }

    public EquatableArray<SerializablePropertyModel> DeserializationProperties { get; }

    public EquatableArray<SerializablePropertyModel> SchemaProperties { get; }

    public EquatableArray<string> BaseTypeIdentities { get; }

    public EquatableArray<string> ComplexPropertyTypeIdentities { get; }

    public bool Equals(SerializableTypeModel? other)
    {
        return other is not null &&
            Type.Equals(other.Type) &&
            Name == other.Name &&
            NamespaceName == other.NamespaceName &&
            SerializerClassName == other.SerializerClassName &&
            HintName == other.HintName &&
            Label == other.Label &&
            Kind == other.Kind &&
            IsRecord == other.IsRecord &&
            HasParameterlessPublicConstructor == other.HasParameterlessPublicConstructor &&
            NeedsConstructor == other.NeedsConstructor &&
            EqualityComparer<ConstructorModel?>.Default.Equals(Constructor, other.Constructor) &&
            SerializationProperties.Equals(other.SerializationProperties) &&
            DeserializationProperties.Equals(other.DeserializationProperties) &&
            SchemaProperties.Equals(other.SchemaProperties) &&
            BaseTypeIdentities.Equals(other.BaseTypeIdentities) &&
            ComplexPropertyTypeIdentities.Equals(other.ComplexPropertyTypeIdentities);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SerializableTypeModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Type.GetHashCode();
            hash = (hash * 31) + Name.GetHashCode();
            hash = (hash * 31) + NamespaceName.GetHashCode();
            hash = (hash * 31) + SerializerClassName.GetHashCode();
            hash = (hash * 31) + HintName.GetHashCode();
            hash = (hash * 31) + Label.GetHashCode();
            hash = (hash * 31) + Kind.GetHashCode();
            hash = (hash * 31) + IsRecord.GetHashCode();
            hash = (hash * 31) + HasParameterlessPublicConstructor.GetHashCode();
            hash = (hash * 31) + NeedsConstructor.GetHashCode();
            hash = (hash * 31) + (Constructor?.GetHashCode() ?? 0);
            hash = (hash * 31) + SerializationProperties.GetHashCode();
            hash = (hash * 31) + DeserializationProperties.GetHashCode();
            hash = (hash * 31) + SchemaProperties.GetHashCode();
            hash = (hash * 31) + BaseTypeIdentities.GetHashCode();
            hash = (hash * 31) + ComplexPropertyTypeIdentities.GetHashCode();
            return hash;
        }
    }
}

internal sealed class TypeDiscoverySet : IEquatable<TypeDiscoverySet>
{
    public TypeDiscoverySet(
        EquatableArray<SerializableTypeModel> roots,
        EquatableArray<SerializableTypeModel> types)
    {
        Roots = roots;
        Types = types;
    }

    public EquatableArray<SerializableTypeModel> Roots { get; }

    public EquatableArray<SerializableTypeModel> Types { get; }

    public static TypeDiscoverySet Empty { get; } = new(
        EquatableArray<SerializableTypeModel>.Empty,
        EquatableArray<SerializableTypeModel>.Empty);

    public static TypeDiscoverySet FromSets(ImmutableArray<TypeDiscoverySet> sets)
    {
        if (sets.IsDefaultOrEmpty)
        {
            return Empty;
        }

        var roots = new List<SerializableTypeModel>();
        var types = new List<SerializableTypeModel>();
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var set in sets)
        {
            Add(roots, seenRoots, set.Roots.Items);
            Add(types, seenTypes, set.Types.Items);
        }

        return new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From(roots),
            EquatableArray<SerializableTypeModel>.From(types));
    }

    public static TypeDiscoverySet Merge(TypeDiscoverySet first, TypeDiscoverySet second)
    {
        return FromSets(ImmutableArray.Create(first, second));
    }

    public bool Equals(TypeDiscoverySet? other)
    {
        return other is not null &&
            Roots.Equals(other.Roots) &&
            Types.Equals(other.Types);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TypeDiscoverySet);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Roots.GetHashCode() * 31) + Types.GetHashCode();
        }
    }

    private static void Add(
        List<SerializableTypeModel> target,
        HashSet<string> seen,
        ImmutableArray<SerializableTypeModel> source)
    {
        foreach (var type in source.Where(type => seen.Add(type.Type.Identity)))
        {
            target.Add(type);
        }
    }
}

internal sealed class GenerationModel : IEquatable<GenerationModel>
{
    private GenerationModel(
        EquatableArray<SerializableTypeModel> roots,
        EquatableArray<SerializableTypeModel> catalog,
        EquatableArray<SerializableTypeModel> allDeclaredRoots)
    {
        Roots = roots;
        Catalog = catalog;
        AllDeclaredRoots = allDeclaredRoots;
    }

    public static GenerationModel FromDiscoverySets(
        TypeDiscoverySet rootsAndCatalog,
        TypeDiscoverySet allDeclaredTypes)
    {
        return new GenerationModel(
            rootsAndCatalog.Roots,
            TypeDiscoverySet.Merge(rootsAndCatalog, allDeclaredTypes).Types,
            allDeclaredTypes.Roots);
    }

    public EquatableArray<SerializableTypeModel> Roots { get; }

    public EquatableArray<SerializableTypeModel> Catalog { get; }

    public EquatableArray<SerializableTypeModel> AllDeclaredRoots { get; }

    public bool Equals(GenerationModel? other)
    {
        return other is not null &&
            Roots.Equals(other.Roots) &&
            Catalog.Equals(other.Catalog) &&
            AllDeclaredRoots.Equals(other.AllDeclaredRoots);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as GenerationModel);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Roots.GetHashCode();
            hash = (hash * 31) + Catalog.GetHashCode();
            hash = (hash * 31) + AllDeclaredRoots.GetHashCode();
            return hash;
        }
    }
}

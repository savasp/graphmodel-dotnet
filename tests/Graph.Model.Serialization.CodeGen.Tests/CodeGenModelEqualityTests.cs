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

namespace Cvoya.Graph.Model.Serialization.CodeGen.Tests;

using Microsoft.CodeAnalysis;


public class CodeGenModelEqualityTests
{
    [Fact]
    public void EquatableArray_UsesSequenceEquality()
    {
        var first = EquatableArray<string>.From(["a", "b"]);
        var second = EquatableArray<string>.From(["a", "b"]);
        var reordered = EquatableArray<string>.From(["b", "a"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, reordered);
    }

    [Fact]
    public void TypeReferenceModel_UsesValueEquality()
    {
        var first = TypeReference();
        var second = TypeReference();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(TypeReferenceInequalityCases))]
    public void TypeReferenceModel_EqualityIncludesEveryField(object variant)
    {
        Assert.NotEqual(TypeReference(), Assert.IsType<TypeReferenceModel>(variant));
    }

    [Fact]
    public void SerializablePropertyModel_UsesValueEquality()
    {
        var first = Property();
        var second = Property();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(PropertyInequalityCases))]
    public void SerializablePropertyModel_EqualityIncludesEveryField(object variant)
    {
        Assert.NotEqual(Property(), Assert.IsType<SerializablePropertyModel>(variant));
    }

    [Fact]
    public void ParameterModel_UsesValueEquality()
    {
        var first = Parameter();
        var second = Parameter();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(ParameterInequalityCases))]
    public void ParameterModel_EqualityIncludesEveryField(object variant)
    {
        Assert.NotEqual(Parameter(), Assert.IsType<ParameterModel>(variant));
    }

    [Fact]
    public void ConstructorModel_UsesStructuralParameterEquality()
    {
        var first = Constructor([Parameter("first"), Parameter("second")]);
        var second = Constructor([Parameter("first"), Parameter("second")]);
        var reordered = Constructor([Parameter("second"), Parameter("first")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, reordered);
    }

    [Fact]
    public void SerializableTypeModel_UsesValueEquality()
    {
        var first = SerializableType();
        var second = SerializableType();

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(SerializableTypeInequalityCases))]
    public void SerializableTypeModel_EqualityIncludesEveryField(object variant)
    {
        Assert.NotEqual(SerializableType(), Assert.IsType<SerializableTypeModel>(variant));
    }

    [Fact]
    public void TypeDiscoverySet_UsesStructuralRootAndTypeEquality()
    {
        var first = new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root")]),
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root"), SerializableType(name: "Nested")]));
        var second = new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root")]),
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root"), SerializableType(name: "Nested")]));
        var changedTypes = new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root")]),
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Root")]));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, changedTypes);
    }

    [Fact]
    public void GenerationModel_UsesStructuralEquality()
    {
        var root = SerializableType(name: "Root");
        var nested = SerializableType(name: "Nested");
        var rootsAndCatalog = new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([root]),
            EquatableArray<SerializableTypeModel>.From([root, nested]));
        var declared = new TypeDiscoverySet(
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Derived")]),
            EquatableArray<SerializableTypeModel>.From([SerializableType(name: "Derived")]));

        var first = GenerationModel.FromDiscoverySets(rootsAndCatalog, declared);
        var second = GenerationModel.FromDiscoverySets(rootsAndCatalog, declared);
        var changed = GenerationModel.FromDiscoverySets(rootsAndCatalog, TypeDiscoverySet.Empty);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, changed);
    }

    public static IEnumerable<object[]> TypeReferenceInequalityCases()
    {
        yield return [TypeReference(identity: "Other.Type")];
        yield return [TypeReference(displayName: "Other.Type")];
        yield return [TypeReference(typeOfName: "Other.Type")];
        yield return [TypeReference(name: "Other")];
        yield return [TypeReference(generatedNamespaceName: "Other.Generated")];
        yield return [TypeReference(serializerClassName: "OtherSerializer")];
        yield return [TypeReference(isReferenceType: false)];
        yield return [TypeReference(isValueType: true)];
        yield return [TypeReference(isNullable: true)];
        yield return [TypeReference(isSchemaNullable: false)];
        yield return [TypeReference(isArray: true)];
        yield return [TypeReference(isSimple: true)];
        yield return [TypeReference(isCollectionOfSimple: true)];
        yield return [TypeReference(isCollectionOfComplex: true)];
        yield return [TypeReference(isEnum: true)];
        yield return [TypeReference(typeKind: TypeKind.Struct)];
        yield return [TypeReference(specialType: SpecialType.System_String)];
        yield return [TypeReference(elementType: TypeReference(identity: "Element.Type", name: "Element"))];
    }

    public static IEnumerable<object[]> PropertyInequalityCases()
    {
        yield return [Property(name: "Other")];
        yield return [Property(label: "other_label")];
        yield return [Property(containingTypeDisplayName: "Other.Container")];
        yield return [Property(containingTypeIsInterface: true)];
        yield return [Property(type: TypeReference(identity: "Other.Type", name: "Other"))];
        yield return [Property(hasSetter: false)];
        yield return [Property(setterIsInitOnly: true)];
        yield return [Property(setterDeclaredPublic: false)];
    }

    public static IEnumerable<object[]> ParameterInequalityCases()
    {
        yield return [Parameter(name: "other")];
        yield return [Parameter(type: TypeReference(identity: "Other.Type", name: "Other"))];
        yield return [Parameter(propertyName: "OtherProperty")];
        yield return [Parameter(hasExplicitDefaultValue: true)];
        yield return [Parameter(explicitDefaultValueExpression: "\"default\"")];
    }

    public static IEnumerable<object[]> SerializableTypeInequalityCases()
    {
        yield return [SerializableType(type: TypeReference(identity: "Other.Type", name: "Other"))];
        yield return [SerializableType(name: "Other")];
        yield return [SerializableType(namespaceName: "Other.Generated")];
        yield return [SerializableType(serializerClassName: "OtherSerializer")];
        yield return [SerializableType(hintName: "OtherSerializer.g.cs")];
        yield return [SerializableType(label: "OtherLabel")];
        yield return [SerializableType(kind: SerializableTypeKind.Relationship)];
        yield return [SerializableType(isRecord: true)];
        yield return [SerializableType(hasParameterlessPublicConstructor: false)];
        yield return [SerializableType(needsConstructor: true)];
        yield return [SerializableType(constructor: Constructor([Parameter(name: "other")]))];
        yield return [SerializableType(serializationProperties: [Property(name: "SerializationOnly")])];
        yield return [SerializableType(deserializationProperties: [Property(name: "DeserializationOnly")])];
        yield return [SerializableType(schemaProperties: [Property(name: "SchemaOnly")])];
        yield return [SerializableType(baseTypeIdentities: ["Base.Type"])];
        yield return [SerializableType(complexPropertyTypeIdentities: ["Complex.Type"])];
    }

    private static TypeReferenceModel TypeReference(
        string identity = "TestNamespace.Type",
        string displayName = "TestNamespace.Type",
        string typeOfName = "TestNamespace.Type",
        string name = "Type",
        string generatedNamespaceName = "TestNamespace.Generated",
        string serializerClassName = "TypeSerializer",
        bool isReferenceType = true,
        bool isValueType = false,
        bool isNullable = false,
        bool isSchemaNullable = true,
        bool isArray = false,
        bool isSimple = false,
        bool isCollectionOfSimple = false,
        bool isCollectionOfComplex = false,
        bool isEnum = false,
        TypeKind typeKind = TypeKind.Class,
        SpecialType specialType = SpecialType.None,
        TypeReferenceModel? elementType = null)
    {
        return new TypeReferenceModel(
            identity,
            displayName,
            typeOfName,
            name,
            generatedNamespaceName,
            serializerClassName,
            isReferenceType,
            isValueType,
            isNullable,
            isSchemaNullable,
            isArray,
            isSimple,
            isCollectionOfSimple,
            isCollectionOfComplex,
            isEnum,
            typeKind,
            specialType,
            elementType);
    }

    private static SerializablePropertyModel Property(
        string name = "Name",
        string label = "name",
        string containingTypeDisplayName = "TestNamespace.Container",
        bool containingTypeIsInterface = false,
        TypeReferenceModel? type = null,
        bool hasSetter = true,
        bool setterIsInitOnly = false,
        bool setterDeclaredPublic = true)
    {
        return new SerializablePropertyModel(
            name,
            label,
            containingTypeDisplayName,
            containingTypeIsInterface,
            type ?? TypeReference(),
            hasSetter,
            setterIsInitOnly,
            setterDeclaredPublic);
    }

    private static ParameterModel Parameter(
        string name = "value",
        TypeReferenceModel? type = null,
        string propertyName = "Value",
        bool hasExplicitDefaultValue = false,
        string explicitDefaultValueExpression = "")
    {
        return new ParameterModel(
            name,
            type ?? TypeReference(),
            propertyName,
            hasExplicitDefaultValue,
            explicitDefaultValueExpression);
    }

    private static ConstructorModel Constructor(ParameterModel[]? parameters = null)
    {
        return new ConstructorModel(EquatableArray<ParameterModel>.From(parameters ?? [Parameter()]));
    }

    private static SerializableTypeModel SerializableType(
        TypeReferenceModel? type = null,
        string name = "Type",
        string? namespaceName = null,
        string? serializerClassName = null,
        string? hintName = null,
        string label = "Type",
        SerializableTypeKind kind = SerializableTypeKind.Node,
        bool isRecord = false,
        bool hasParameterlessPublicConstructor = true,
        bool needsConstructor = false,
        ConstructorModel? constructor = null,
        SerializablePropertyModel[]? serializationProperties = null,
        SerializablePropertyModel[]? deserializationProperties = null,
        SerializablePropertyModel[]? schemaProperties = null,
        string[]? baseTypeIdentities = null,
        string[]? complexPropertyTypeIdentities = null)
    {
        var modelType = type ?? TypeReference(identity: $"TestNamespace.{name}", name: name);

        return new SerializableTypeModel(
            modelType,
            name,
            namespaceName ?? $"{name}.Generated",
            serializerClassName ?? $"{name}Serializer",
            hintName ?? $"{name}Serializer.g.cs",
            label,
            kind,
            isRecord,
            hasParameterlessPublicConstructor,
            needsConstructor,
            constructor ?? Constructor(),
            EquatableArray<SerializablePropertyModel>.From(serializationProperties ?? [Property()]),
            EquatableArray<SerializablePropertyModel>.From(deserializationProperties ?? [Property()]),
            EquatableArray<SerializablePropertyModel>.From(schemaProperties ?? [Property()]),
            EquatableArray<string>.From(baseTypeIdentities ?? []),
            EquatableArray<string>.From(complexPropertyTypeIdentities ?? []));
    }
}

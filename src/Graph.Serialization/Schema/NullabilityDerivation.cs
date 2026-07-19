// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Reflection;
using System.Runtime.CompilerServices;
using Cvoya.Graph.Serialization.Results;


/// <summary>
/// Derives declared nullability from compiler metadata.
/// </summary>
/// <remarks>
/// Property schemas and constructor-parameter materialization both read nullability from here so
/// their collection-element contracts cannot drift. Nullable value types are nullable, other value
/// types are not, and reference types follow nullable-reference metadata. Collection elements whose
/// declaration carries no metadata fail closed.
/// <para>
/// Nullable annotations on an unconstrained generic parameter are runtime-indistinguishable:
/// reflection reports both <c>T</c> and <c>T?</c> as nullable after the declaring type is closed.
/// Such parameters therefore fail closed. A reference-type constraint preserves enough metadata to
/// distinguish <c>T</c> from <c>T?</c>, and nullable value-type arguments remain distinguishable by
/// their constructed <see cref="Nullable{T}"/> type.
/// </para>
/// </remarks>
internal static class NullabilityDerivation
{
    /// <summary>Derives whether a declared constructor parameter may be null.</summary>
    public static bool IsParameterNullable(ParameterInfo parameter) =>
        ForParameter(parameter).AllowsNull(unknownIsNullable: true);

    /// <summary>Derives whether the elements of a declared property may be null.</summary>
    public static bool IsElementNullable(PropertyInfo property, Type? elementType) =>
        ForProperty(property).GetElement(elementType)?.AllowsNull(unknownIsNullable: false) ?? false;

    /// <summary>Derives whether the elements of a declared constructor parameter may be null.</summary>
    public static bool IsElementNullable(ParameterInfo parameter, Type? elementType) =>
        ForParameter(parameter).GetElement(elementType)?.AllowsNull(unknownIsNullable: false) ?? false;

    /// <summary>Builds the recursive nullability declaration for a constructor parameter.</summary>
    public static NullabilityDeclaration ForParameter(ParameterInfo parameter)
    {
        var nullability = new NullabilityInfoContext().Create(parameter);
        var definitionParameter = GetDefinitionParameter(parameter);
        return new NullabilityDeclaration(
            parameter.ParameterType,
            nullability,
            definitionParameter?.ParameterType,
            definitionParameter is null ? null : new NullabilityInfoContext().Create(definitionParameter),
            AllowsNullOverride: IsAnonymousType(parameter.Member.DeclaringType) && !parameter.ParameterType.IsValueType
                ? true
                : null);
    }

    private static NullabilityDeclaration ForProperty(PropertyInfo property)
    {
        var nullability = new NullabilityInfoContext().Create(property);
        var definitionProperty = GetDefinitionProperty(property);
        return new NullabilityDeclaration(
            property.PropertyType,
            nullability,
            definitionProperty?.PropertyType,
            definitionProperty is null ? null : new NullabilityInfoContext().Create(definitionProperty),
            AllowsNullOverride: null);
    }

    private static bool IsAnonymousType(Type? type) =>
        type is { IsSealed: true, IsNotPublic: true } &&
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) &&
        type.Name.Contains("AnonymousType", StringComparison.Ordinal);

    private static ParameterInfo? GetDefinitionParameter(ParameterInfo parameter)
    {
        var member = parameter.Member;
        var declaringType = member.DeclaringType;
        if (declaringType is not { IsConstructedGenericType: true })
        {
            return null;
        }

        var definitionType = declaringType.GetGenericTypeDefinition();
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var definitionMember = definitionType
            .GetMembers(bindingFlags)
            .OfType<MethodBase>()
            .FirstOrDefault(candidate => candidate.MetadataToken == member.MetadataToken);

        return definitionMember?.GetParameters()
            .FirstOrDefault(candidate => candidate.Position == parameter.Position);
    }

    private static PropertyInfo? GetDefinitionProperty(PropertyInfo property)
    {
        var declaringType = property.DeclaringType;
        if (declaringType is not { IsConstructedGenericType: true })
        {
            return null;
        }

        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        return declaringType.GetGenericTypeDefinition()
            .GetProperties(bindingFlags)
            .FirstOrDefault(candidate => candidate.MetadataToken == property.MetadataToken);
    }
}

/// <summary>Recursive nullable metadata for a closed declaration and its generic definition.</summary>
internal sealed record NullabilityDeclaration(
    Type Type,
    NullabilityInfo Nullability,
    Type? DefinitionType,
    NullabilityInfo? DefinitionNullability,
    bool? AllowsNullOverride)
{
    /// <summary>Gets whether this level of the declaration admits null.</summary>
    public bool AllowsNull(bool unknownIsNullable)
    {
        if (AllowsNullOverride is { } allowsNull)
        {
            return allowsNull;
        }

        if (Nullable.GetUnderlyingType(Type) is not null)
        {
            return true;
        }

        if (Type.IsValueType)
        {
            return false;
        }

        if (DefinitionType is { IsGenericParameter: true } genericParameter)
        {
            var constraints = genericParameter.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
            if ((constraints & (GenericParameterAttributes.ReferenceTypeConstraint |
                                GenericParameterAttributes.NotNullableValueTypeConstraint)) == 0)
            {
                return false;
            }
        }

        return Nullability.ReadState switch
        {
            NullabilityState.Nullable => true,
            NullabilityState.Unknown => unknownIsNullable,
            _ => false,
        };
    }

    /// <summary>Gets the declaration for this collection's element type.</summary>
    public NullabilityDeclaration? GetElement(Type? elementType)
    {
        if (elementType is null)
        {
            return null;
        }

        var elementNullability = GetElementNullability(Type, Nullability);
        if (elementNullability is null)
        {
            return null;
        }

        Type? definitionElementType = null;
        NullabilityInfo? definitionElementNullability = null;
        if (DefinitionType is not null && DefinitionNullability is not null)
        {
            definitionElementType = GraphResultTypeHelpers.GetCollectionElementType(DefinitionType);
            definitionElementNullability = GetElementNullability(DefinitionType, DefinitionNullability);
        }

        return new NullabilityDeclaration(
            elementType,
            elementNullability,
            definitionElementType,
            definitionElementNullability,
            AllowsNullOverride: null);
    }

    private static NullabilityInfo? GetElementNullability(Type collectionType, NullabilityInfo nullability)
    {
        if (collectionType.IsArray)
        {
            return nullability.ElementType;
        }

        var elementType = GraphResultTypeHelpers.GetCollectionElementType(collectionType);
        if (elementType is null)
        {
            return null;
        }

        var arguments = collectionType.IsGenericType
            ? collectionType.GetGenericArguments()
            : [];

        if (collectionType.IsGenericType)
        {
            var definitionElementType = GraphResultTypeHelpers.GetCollectionElementType(
                collectionType.GetGenericTypeDefinition());
            if (definitionElementType is { IsGenericParameter: true } genericElement &&
                genericElement.DeclaringType == collectionType.GetGenericTypeDefinition() &&
                genericElement.GenericParameterPosition < nullability.GenericTypeArguments.Length)
            {
                return nullability.GenericTypeArguments[genericElement.GenericParameterPosition];
            }
        }

        for (var index = 0; index < arguments.Length && index < nullability.GenericTypeArguments.Length; index++)
        {
            if (arguments[index] == elementType)
            {
                return nullability.GenericTypeArguments[index];
            }
        }

        return nullability.GenericTypeArguments.Length == 1
            ? nullability.GenericTypeArguments[0]
            : null;
    }
}

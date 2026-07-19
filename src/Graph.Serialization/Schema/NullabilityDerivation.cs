// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

using System.Reflection;


/// <summary>
/// Derives declared element nullability from compiler metadata.
/// </summary>
/// <remarks>
/// Property schemas and constructor-parameter materialization both read element nullability from
/// here so the two cannot drift: <see cref="Nullable{T}"/> elements are nullable, any other value
/// type is not, and reference elements follow nullable-reference metadata. Elements whose
/// declaration carries no such metadata are treated as non-nullable, which fails closed rather than
/// admitting a null the declaration never promised.
/// <para>
/// Unconstrained generic type parameters are the exception, and they fail open: nullable metadata is
/// resolved against the generic type definition, where such a parameter is encoded as maybe-null, so
/// every instantiation reads as nullable no matter what the type argument is. Closing that needs the
/// type argument substituted in before the state is read — see #435.
/// </para>
/// </remarks>
internal static class NullabilityDerivation
{
    /// <summary>Derives whether the elements of a declared property may be null.</summary>
    public static bool IsElementNullable(PropertyInfo property, Type? elementType) =>
        IsElementNullable(elementType, () => (property.PropertyType, new NullabilityInfoContext().Create(property)));

    /// <summary>Derives whether the elements of a declared constructor parameter may be null.</summary>
    public static bool IsElementNullable(ParameterInfo parameter, Type? elementType) =>
        IsElementNullable(elementType, () => (parameter.ParameterType, new NullabilityInfoContext().Create(parameter)));

    // The declaration is resolved through a callback so reading nullable metadata — which allocates a
    // NullabilityInfoContext — is skipped entirely for the element types that answer from the type alone.
    private static bool IsElementNullable(
        Type? elementType,
        Func<(Type DeclaredType, NullabilityInfo Nullability)> declaration)
    {
        if (elementType is null)
        {
            return false;
        }

        if (Nullable.GetUnderlyingType(elementType) is not null)
        {
            return true;
        }

        if (elementType.IsValueType)
        {
            return false;
        }

        var (declaredType, nullability) = declaration();
        var elementNullability = declaredType.IsArray
            ? nullability.ElementType
            : nullability.GenericTypeArguments.FirstOrDefault();

        return elementNullability?.ReadState == NullabilityState.Nullable;
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

internal static class QueryModelGuard
{
    public static IReadOnlyList<T> CopyRequiredList<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = new T[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            copy[i] = values[i] ?? throw new ArgumentException($"The {parameterName} collection cannot contain null elements.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    public static void RequireNullOrNotWhiteSpace(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }
    }

    public static void RequireDefinedEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Unknown {typeof(T).Name} value.");
        }
    }

    public static void RequireAssignableTo(Type type, Type expectedBaseType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(type, parameterName);

        if (!expectedBaseType.IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type '{type.FullName}' must be assignable to '{expectedBaseType.FullName}'.", parameterName);
        }
    }
}

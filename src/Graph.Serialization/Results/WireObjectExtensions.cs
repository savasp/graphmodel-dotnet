// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Globalization;

namespace Cvoya.Graph.Serialization.Results;

internal static class WireObjectExtensions
{
    public static T As<T>(this object? value)
    {
        var converted = value is GraphValue graphValue ? graphValue.ToObject() : value;
        if (converted is T typed)
        {
            return typed;
        }

        if (converted is null)
        {
            return default!;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType.IsEnum)
        {
            return (T)(converted is string text
                ? Enum.Parse(targetType, text, ignoreCase: true)
                : Enum.ToObject(targetType, converted));
        }

        return (T)Convert.ChangeType(converted, targetType, CultureInfo.InvariantCulture);
    }
}

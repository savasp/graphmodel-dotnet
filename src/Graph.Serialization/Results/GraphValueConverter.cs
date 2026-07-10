// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections;
using System.Globalization;

namespace Cvoya.Graph.Serialization.Results;

internal static class GraphValueConverter
{
    internal const string MetadataPropertyName = "__metadata__";
    internal const string EntityKindPropertyName = "__graphModelEntityKind__";
    private const string TypeNameKey = "type";

    public static object? ConvertTo(object? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (value is null)
        {
            return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null
                ? Activator.CreateInstance(targetType)
                : null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlyingType.IsInstanceOfType(value))
        {
            return value;
        }

        if (underlyingType == typeof(string))
            return value.ToString();
        if (underlyingType == typeof(char))
            return System.Convert.ToChar(value, CultureInfo.InvariantCulture);
        if (underlyingType.IsEnum)
        {
            return value is string text
                ? Enum.Parse(underlyingType, text, ignoreCase: true)
                : Enum.ToObject(underlyingType, value);
        }

        if (underlyingType == typeof(DateTime))
        {
            return value switch
            {
                DateTimeOffset offset => offset.UtcDateTime,
                DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                _ => System.Convert.ToDateTime(value, CultureInfo.InvariantCulture),
            };
        }

        if (underlyingType == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                _ => DateTimeOffset.Parse(value.ToString()!, CultureInfo.InvariantCulture),
            };
        }

        if (underlyingType == typeof(DateOnly))
        {
            return value is DateTime dateTime
                ? DateOnly.FromDateTime(dateTime)
                : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(TimeOnly))
        {
            return value switch
            {
                DateTimeOffset offset => TimeOnly.FromTimeSpan(offset.TimeOfDay),
                DateTime dateTime => TimeOnly.FromDateTime(dateTime),
                TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
                long ticks => new TimeOnly(ticks),
                _ => TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
            };
        }

        if (underlyingType == typeof(TimeSpan))
        {
            return value switch
            {
                double milliseconds => TimeSpan.FromMilliseconds(milliseconds),
                long milliseconds => TimeSpan.FromMilliseconds(milliseconds),
                _ => TimeSpan.Parse(value.ToString()!, CultureInfo.InvariantCulture),
            };
        }

        if (underlyingType == typeof(Guid))
            return Guid.Parse(value.ToString()!);
        if (underlyingType == typeof(Uri))
            return new Uri(value.ToString()!);

        if (TryConvertCollection(value, underlyingType, out var collection))
        {
            return collection;
        }

        return System.Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }

    public static Type? GetTypeFromMetadata(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null ||
            !properties.TryGetValue(MetadataPropertyName, out var metadata) ||
            metadata is not IReadOnlyDictionary<string, object> map ||
            !map.TryGetValue(TypeNameKey, out var typeName) ||
            typeName is not string text)
        {
            return null;
        }

        return Type.GetType(text);
    }

    private static bool TryConvertCollection(object value, Type targetType, out object? result)
    {
        result = null;
        if (value is not IEnumerable source || value is string)
        {
            return false;
        }

        var elementType = targetType.IsArray
            ? targetType.GetElementType()
            : targetType.IsGenericType
                ? targetType.GetGenericArguments().FirstOrDefault()
                : null;
        if (elementType is null)
        {
            return false;
        }

        var values = source.Cast<object?>().Select(item => ConvertTo(item, elementType)).ToArray();
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                array.SetValue(values[index], index);
            }

            result = array;
            return true;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in values)
        {
            list.Add(item);
        }

        if (!targetType.IsAssignableFrom(listType))
        {
            return false;
        }

        result = list;
        return true;
    }
}

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
        return ConvertTo(value, targetType, propertyName: null, isElementNullable: null);
    }

    public static object? ConvertTo(
        object? value,
        Type targetType,
        string propertyName,
        bool isElementNullable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return ConvertTo(value, targetType, propertyName, (bool?)isElementNullable);
    }

    private static object? ConvertTo(
        object? value,
        Type targetType,
        string? propertyName,
        bool? isElementNullable)
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
            ValidateNullElements(value, underlyingType, propertyName, isElementNullable);
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

        if (TryConvertCollection(
            value,
            underlyingType,
            propertyName,
            isElementNullable,
            out var collection))
        {
            return collection;
        }

        return System.Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }

    public static Type? GetTypeFromMetadata(IReadOnlyDictionary<string, object>? properties)
    {
        if (properties is null || !properties.TryGetValue(MetadataPropertyName, out var metadata))
        {
            return null;
        }

        // The provider-neutral wire convention is { "type": "assembly-qualified name" }.
        // Backends such as Neo4j cannot persist map-valued entity properties, so providers may
        // store the assembly-qualified name as a scalar under the same reserved property instead.
        var typeName = metadata switch
        {
            string scalar => scalar,
            IReadOnlyDictionary<string, object> map when map.TryGetValue(TypeNameKey, out var value) => value as string,
            _ => null,
        };

        return typeName is null ? null : Type.GetType(typeName);
    }

    private static bool TryConvertCollection(
        object value,
        Type targetType,
        string? propertyName,
        bool? isElementNullable,
        out object? result)
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

        var values = source.Cast<object?>()
            .Select((item, index) =>
            {
                if (item is null && isElementNullable == false)
                {
                    throw CreateNullElementException(propertyName!, elementType, index);
                }

                return ConvertTo(item, elementType);
            })
            .ToArray();
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

    private static void ValidateNullElements(
        object value,
        Type targetType,
        string? propertyName,
        bool? isElementNullable)
    {
        if (isElementNullable != false ||
            propertyName is null ||
            value is not IEnumerable source ||
            value is string)
        {
            return;
        }

        var elementType = targetType.IsArray
            ? targetType.GetElementType()
            : targetType.IsGenericType
                ? targetType.GetGenericArguments().FirstOrDefault()
                : null;
        if (elementType is null)
        {
            return;
        }

        // A CLR collection of a non-nullable value type cannot contain null, so scanning it
        // (boxing every element — e.g. each byte of a byte[] blob property) proves nothing.
        if (elementType.IsValueType && Nullable.GetUnderlyingType(elementType) is null)
        {
            return;
        }

        var index = 0;
        foreach (var item in source)
        {
            if (item is null)
            {
                throw CreateNullElementException(propertyName, elementType, index);
            }

            index++;
        }
    }

    private static GraphException CreateNullElementException(
        string propertyName,
        Type elementType,
        int index)
    {
        return new GraphException(
            $"Collection property '{propertyName}' contains a null element at index {index}, " +
            $"but its target element type '{elementType}' is non-nullable.");
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Execution;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using Npgsql.Age.Types;

internal static class AgeValueExtensions
{
    public static T As<T>(this object? value)
    {
        var converted = ConvertValue(value, typeof(T));
        return converted is null ? default! : (T)converted;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (value is null)
        {
            return null;
        }

        if (value is Agtype agtype)
        {
            if (agtype.IsNull)
            {
                return null;
            }

            if (agtype.IsArray)
            {
                value = agtype.GetList();
            }
            else if (agtype.IsMap)
            {
                value = agtype.GetMap();
            }
            else if (effectiveType == typeof(string))
            {
                return agtype.ToString().StartsWith('"') ? agtype.GetString() : agtype.ToString();
            }
            else
            {
                value = ParseScalar(agtype.ToString());
            }
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        if (effectiveType.IsEnum)
        {
            return Enum.Parse(effectiveType, Convert.ToString(value, CultureInfo.InvariantCulture)!, ignoreCase: true);
        }

        if (effectiveType == typeof(Guid))
        {
            return Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        }

        if (effectiveType == typeof(DateTime))
        {
            return DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (effectiveType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
        }

        if (effectiveType.IsGenericType && effectiveType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = effectiveType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(effectiveType)!;
            if (value is not IEnumerable sequence)
            {
                throw new InvalidCastException($"Cannot convert '{value?.GetType().Name ?? "null"}' to {effectiveType.Name}.");
            }

            foreach (var item in sequence)
            {
                list.Add(ConvertValue(item is JsonElement json ? JsonValue(json) : item, elementType));
            }

            return list;
        }

        return Convert.ChangeType(value is JsonElement element ? JsonValue(element) : value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static object? ParseScalar(string text)
    {
        text = text.Trim();
        if (text.EndsWith("::numeric", StringComparison.Ordinal))
        {
            text = text[..^9];
        }

        if (text.StartsWith('"'))
        {
            return JsonSerializer.Deserialize<string>(text);
        }

        // agtype booleans are exactly "true"/"false"; a lenient parse would misread a genuine
        // string value such as "True". Keep the token matching strict, like AgeRecordAdapter.
        if (text is "true") return true;
        if (text is "false") return false;
        if (text is "NaN") return double.NaN;
        if (text is "Infinity") return double.PositiveInfinity;
        if (text is "-Infinity") return double.NegativeInfinity;
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
        return text;
    }

    private static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        _ => value.GetRawText(),
    };
}

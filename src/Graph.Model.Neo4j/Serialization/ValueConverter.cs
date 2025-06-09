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

using System.Collections;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

internal class ValueConverter
{
    private const int WGS84_SRID = 4326;

    public object? ConvertToNeo4j(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b,
        byte b => (long)b,
        sbyte sb => (long)sb,
        short s => (long)s,
        ushort us => (long)us,
        int i => (long)i,
        uint ui => (long)ui,
        long l => l,
        ulong ul => (long)ul,
        float f => (double)f,
        double d => d,
        decimal dec => (double)dec,
        DateTime dt => new ZonedDateTime(dt),
        DateTimeOffset dto => new ZonedDateTime(dto),
        TimeSpan ts => new LocalTime(ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds * 1_000_000),
        TimeOnly to => new LocalTime(to.Hour, to.Minute, to.Second, to.Nanosecond),
        DateOnly d => new LocalDate(d.Year, d.Month, d.Day),
        Guid g => g.ToString(),
        Uri uri => uri.ToString(),
        Enum e => e.ToString(),
        byte[] bytes => bytes,
        Model.Point p => new Point(p.X, p.Y, p.Z),
        IEnumerable enumerable => ConvertCollection(enumerable),
        _ => throw new NotSupportedException($"Type {value.GetType()} is not supported for Neo4j conversion")
    };

    public object? ConvertFromNeo4j(object? value, Type targetType)
    {
        if (value == null)
            return null;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return value switch
        {
            _ when underlyingType == typeof(string) => value.ToString(),
            _ when underlyingType == typeof(bool) => Convert.ToBoolean(value),
            _ when underlyingType == typeof(byte) => Convert.ToByte(value),
            _ when underlyingType == typeof(sbyte) => Convert.ToSByte(value),
            _ when underlyingType == typeof(short) => Convert.ToInt16(value),
            _ when underlyingType == typeof(ushort) => Convert.ToUInt16(value),
            _ when underlyingType == typeof(int) => Convert.ToInt32(value),
            _ when underlyingType == typeof(uint) => Convert.ToUInt32(value),
            _ when underlyingType == typeof(long) => Convert.ToInt64(value),
            _ when underlyingType == typeof(ulong) => Convert.ToUInt64(value),
            _ when underlyingType == typeof(float) => Convert.ToSingle(value),
            _ when underlyingType == typeof(double) => Convert.ToDouble(value),
            _ when underlyingType == typeof(decimal) => Convert.ToDecimal(value),
            _ when underlyingType == typeof(DateTime) => ConvertToDateTime(value),
            _ when underlyingType == typeof(DateTimeOffset) => ConvertToDateTimeOffset(value),
            _ when underlyingType == typeof(TimeSpan) => ConvertToTimeSpan(value),
            _ when underlyingType == typeof(TimeOnly) => ConvertToTimeOnly(value),
            _ when underlyingType == typeof(DateOnly) => ConvertToDateOnly(value),
            _ when underlyingType == typeof(Guid) => Guid.Parse(value.ToString()!),
            _ when underlyingType == typeof(Uri) => new Uri(value.ToString()!),
            _ when underlyingType.IsEnum => Enum.Parse(underlyingType, value.ToString()!),
            _ when underlyingType == typeof(byte[]) => (byte[])value,
            _ when underlyingType == typeof(Model.Point) => ConvertToPoint(value),
            _ when IsCollectionType(underlyingType) => ConvertFromCollection(value, underlyingType),
            _ => throw new NotSupportedException($"Cannot convert from Neo4j value to type {targetType}")
        };
    }

    private object ConvertCollection(IEnumerable enumerable)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(ConvertToNeo4j(item));
        }
        return list;
    }

    private object? ConvertFromCollection(object value, Type targetType)
    {
        if (value is not IEnumerable enumerable)
            return null;

        var elementType = targetType.IsArray
            ? targetType.GetElementType()!
            : targetType.GetGenericArguments()[0];

        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(ConvertFromNeo4j(item, elementType));
        }

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                array.SetValue(list[i], i);
            }
            return array;
        }

        var genericListType = typeof(List<>).MakeGenericType(elementType);
        var typedList = Activator.CreateInstance(genericListType) as IList;
        foreach (var item in list)
        {
            typedList!.Add(item);
        }

        return typedList;
    }

    private static bool IsCollectionType(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static DateTime ConvertToDateTime(object value) => value switch
    {
        ZonedDateTime zdt => zdt.ToDateTimeOffset().DateTime,
        LocalDateTime ldt => ldt.ToDateTime(),
        LocalDate ld => ld.ToDateTime(),
        _ => Convert.ToDateTime(value)
    };

    private static DateTimeOffset ConvertToDateTimeOffset(object value) => value switch
    {
        ZonedDateTime zdt => zdt.ToDateTimeOffset(),
        LocalDateTime ldt => new DateTimeOffset(ldt.ToDateTime()),
        _ => new DateTimeOffset(Convert.ToDateTime(value))
    };

    private static TimeSpan ConvertToTimeSpan(object value) => value switch
    {
        LocalTime lt => new TimeSpan(0, lt.Hour, lt.Minute, lt.Second, lt.Nanosecond / 1_000_000),
        _ => TimeSpan.Parse(value.ToString()!)
    };

    private static TimeOnly ConvertToTimeOnly(object value) => value switch
    {
        LocalTime lt => new TimeOnly(lt.Hour, lt.Minute, lt.Second, lt.Nanosecond / 1_000_000),
        _ => TimeOnly.Parse(value.ToString()!)
    };

    private static DateOnly ConvertToDateOnly(object value) => value switch
    {
        LocalDate ld => new DateOnly(ld.Year, ld.Month, ld.Day),
        LocalDateTime ldt => DateOnly.FromDateTime(ldt.ToDateTime()),
        ZonedDateTime zdt => DateOnly.FromDateTime(zdt.ToDateTimeOffset().DateTime),
        _ => DateOnly.Parse(value.ToString()!)
    };

    private static Model.Point ConvertToPoint(object value) => value switch
    {
        Point p => new Model.Point { X = p.X, Y = p.Y, Z = p.Z },
        _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to Point")
    };
}
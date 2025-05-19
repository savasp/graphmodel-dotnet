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
using System.Reflection;
using Cvoya.Graph.Provider.Model;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

internal static class SerializationExtensions
{
    private const int WGS84 = 4326;

    public static (Dictionary<PropertyInfo, object?>, Dictionary<PropertyInfo, object?>) GetSimpleAndComplexProperties(object obj)
    {
        var simpleProperties = new Dictionary<PropertyInfo, object?>();
        var complexProperties = new Dictionary<PropertyInfo, object?>();

        var properties = obj.GetType().GetProperties();

        foreach (var property in properties)
        {
            var labelName = property.GetCustomAttribute<PropertyAttribute>()?.Label ?? property.Name;

            switch (property)
            {
                // This must come first!
                case { PropertyType: var type } when type.IsRelationshipType() || type.IsCollectionOfRelationshipType():
                    continue;

                case { PropertyType: var type } when type.IsPrimitiveOrSimple() || type.IsCollectionOfSimple():
                    simpleProperties[property] = property.GetValue(obj);
                    break;

                default:
                    complexProperties[property] = property.GetValue(obj);
                    break;
            }
        }

        return (simpleProperties, complexProperties);
    }

    public static Dictionary<string, object?> ConvertToNeo4jProperties(this object obj)
    {
        var (simpleProperties, _) = GetSimpleAndComplexProperties(obj);

        var result = new Dictionary<string, object?>();
        foreach (var kvp in simpleProperties)
        {
            var name = kvp.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? kvp.Key.Name;
            result[name] = ConvertToNeo4jValue(kvp.Value);
        }

        return result;
    }

    public static Dictionary<string, object?> ConvertToDomainTypeProperties<T>() where T : new()
    {
        var obj = new T();
        var (simpleProperties, _) = GetSimpleAndComplexProperties(obj);

        var result = new Dictionary<string, object?>();
        foreach (var kvp in simpleProperties)
        {
            var name = kvp.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? kvp.Key.Name;
            result[name] = ConvertFromNeo4jValue(kvp.Value, simpleProperties[kvp.Key]!.GetType());
        }

        return result;
    }

    public static object? ConvertToNeo4jValue(this object? value) => value switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        TimeSpan ts => ts,
        TimeOnly to => to.ToTimeSpan(),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        Provider.Model.Point point => new global::Neo4j.Driver.Point(WGS84, point.X, point.Y, point.Z),
        IDictionary dict => dict.Cast<DictionaryEntry>()
                                .ToDictionary(
                                    entry => entry.Key.ToString() ?? "",
                                    entry => ConvertToNeo4jValue(entry.Value)),
        IEnumerable collection when value is not string =>
            collection.Cast<object?>().Select(ConvertToNeo4jValue).ToArray(),
        Enum e => e.ToString(),
        _ => value
    };

    public static T ConvertToGraphEntity<T>(this global::Neo4j.Driver.IEntity entity) where T : new()
    {
        var obj = new T();
        var properties = typeof(T).GetProperties();
        var labelsToProperties = properties.ToDictionary(
            p => p.GetCustomAttribute<PropertyAttribute>()?.Label ?? p.Name,
            p => p);

        foreach (var p in entity.Properties)
        {
            if (labelsToProperties.TryGetValue(p.Key, out var property))
            {
                var value = p.Value;

                if (value is null)
                    continue;

                if (property.PropertyType.IsPrimitiveOrSimple())
                    property.SetValue(obj, ConvertFromNeo4jValue(value, property.PropertyType));
                else
                    continue;
            }
        }

        return obj;
    }

    public static object? ConvertFromNeo4jValue(this object? value, Type targetType)
    {
        if (value is null)
            return null;

        if (targetType.IsInstanceOfType(value))
            return value;

        return targetType switch
        {
            Type t when t == typeof(DateTime) => value switch
            {
                ZonedDateTime zdt => zdt.ToDateTimeOffset().DateTime,
                LocalDateTime ldt => ldt.ToDateTime(),
                LocalDate ld => ld.ToDateTime(),
                _ => Convert.ChangeType(value, targetType)
            },
            Type t when t == typeof(DateTimeOffset) && value is ZonedDateTime zdt2 => zdt2.ToDateTimeOffset(),
            Type t when t == typeof(TimeOnly) && value is LocalTime lt2 => TimeOnly.FromTimeSpan(lt2.ToTimeSpan()),
            Type t when t == typeof(DateOnly) && value is LocalDate ld2 => DateOnly.FromDateTime(ld2.ToDateTime()),
            Type t when t.IsEnum && value is string enumString => Enum.Parse(targetType, enumString),
            Type t when t == typeof(Provider.Model.Point) && value is global::Neo4j.Driver.Point point => new Model.Point(point.X, point.Y, point.Z),
            _ => Convert.ChangeType(value, targetType)
        };
    }

    public static bool IsRelationshipType(this Type type) =>
        type.GetInterfaces().Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(Cvoya.Graph.Provider.Model.IRelationship));

    public static bool IsCollectionOfRelationshipType(this Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => type.GetElementType()!.IsRelationshipType(),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && arg.IsRelationshipType(),
            _ => false
        };

    public static bool IsPrimitiveOrSimple(this Type type) => type switch
    {
        _ when type.IsPrimitive => true,
        _ when type.IsEnum => true,
        _ when type == typeof(string) => true,
        _ when type.IsValueType => true,
        _ when type == typeof(Provider.Model.Point) => true,
        _ => false
    };

    public static bool IsCollectionOfSimple(this Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => type.GetElementType()!.IsPrimitiveOrSimple(),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && arg.IsPrimitiveOrSimple(),
            _ => false
        };


    public static bool IsGraphNodeSerializable(this Type type) => type switch
    {
        _ when type.IsPrimitiveOrSimple() => true,
        _ when type.IsCollectionOfSimple() => true,
        _ when type.IsRelationshipType() => true,
        _ => false
    };

    public static bool IsGraphRelationshipSerializable(this Type type) => type switch
    {
        _ when type.IsPrimitiveOrSimple() => true,
        _ when type.IsCollectionOfSimple() => true,
        _ => false
    };

    public static Dictionary<string, object?> ConvertPropertiesToNeo4j(Dictionary<PropertyInfo, object?> props)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in props)
        {
            var name = kvp.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? kvp.Key.Name;
            result[name] = ConvertToNeo4jValue(kvp.Value);
        }
        return result;
    }
}
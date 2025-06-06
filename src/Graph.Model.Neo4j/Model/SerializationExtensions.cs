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
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Conversion;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;
/*
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
            switch (property)
            {
                // This must come first!
                case { PropertyType: var t } when Helpers.IsRelationshipType(t) || Helpers.IsCollectionOfRelationshipType(t):
                    continue;
                // This must come second!
                case { PropertyType: var t } when t.IsPrimitiveOrSimple() || t.IsCollectionOfSimple():
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

    public static object? ConvertToNeo4jValue(this object? value) => value switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto,
        TimeSpan ts => ts,
        TimeOnly to => to.ToTimeSpan(),
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        decimal d => (double)d, // Convert decimal to double for Neo4j storage
        float f => (double)f,   // Convert float to double for Neo4j storage
        Model.Point point => new global::Neo4j.Driver.Point(WGS84, point.X, point.Y, point.Z),
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
                {
                    continue;
                }

                if (property.PropertyType.IsPrimitiveOrSimple())
                {
                    property.SetValue(obj, ConvertFromNeo4jValue(value, property.PropertyType));
                }
                else
                {
                    continue;
                }
            }
        }

        // Handle ID property for nodes and relationships
        if (entity is global::Neo4j.Driver.INode node)
        {
            var idProperty = properties.FirstOrDefault(p => p.Name == "Id");
            if (idProperty != null && idProperty.PropertyType == typeof(string) && node.Properties.ContainsKey("Id"))
            {
                idProperty.SetValue(obj, node.Properties["Id"].As<string>());
            }
        }
        else if (entity is global::Neo4j.Driver.IRelationship relationship)
        {
            var idProperty = properties.FirstOrDefault(p => p.Name == "Id");
            if (idProperty != null && idProperty.PropertyType == typeof(string))
            {
                // First try to get the Id from the relationship properties
                if (relationship.Properties.ContainsKey("Id"))
                {
                    idProperty.SetValue(obj, relationship.Properties["Id"].As<string>());
                }
                else
                {
                    // Fall back to ElementId if no Id property exists
                    idProperty.SetValue(obj, relationship.ElementId);
                }
            }

            // Don't set SourceId and TargetId here - they will be set by the query
            // that includes the source and target nodes
        }
        else
        {
            throw new InvalidOperationException($"Entity type: {entity.GetType().Name}.");
        }
        return obj;
    }

    private static readonly Neo4jEntityConverter _converter = new(null);

    public static object? ConvertFromNeo4jValue(this object? value, Type targetType)
    {
        // Special handling for node/relationship types that need ConvertToGraphEntity
        if (value is not null)
        {
            if (typeof(Model.IRelationship).IsAssignableFrom(targetType) && value is Model.IRelationship rel)
            {
                var method = typeof(SerializationExtensions).GetMethod(nameof(ConvertToGraphEntity), BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                var generic = method!.MakeGenericMethod(targetType);
                return generic.Invoke(null, [rel]);
            }

            if (typeof(Model.INode).IsAssignableFrom(targetType) && value is Model.INode node)
            {
                var method = typeof(SerializationExtensions).GetMethod(nameof(ConvertToGraphEntity), BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                var generic = method!.MakeGenericMethod(targetType);
                return generic.Invoke(null, [node]);
            }

            // Handle Neo4j INode to POCO mapping
            if (value is global::Neo4j.Driver.INode neo4jNode && targetType.IsClass && targetType != typeof(string))
            {
                var nodeDict = neo4jNode.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                return ConvertFromNeo4jValue(nodeDict, targetType);
            }

            // Handle dictionary-to-object mapping for POCOs
            if (value is IDictionary<string, object> dict && targetType.IsClass && targetType != typeof(string))
            {
                var obj = Activator.CreateInstance(targetType);
                var properties = targetType.GetProperties();
                foreach (var prop in properties)
                {
                    if (dict.TryGetValue(prop.Name, out var propValue) && propValue != null)
                    {
                        if (prop.PropertyType.IsPrimitiveOrSimple())
                            prop.SetValue(obj, ConvertFromNeo4jValue(propValue, prop.PropertyType));
                    }
                }
                return obj;
            }
        }

        // Delegate all other conversions to the converter
        return _converter.ConvertFromNeo4jValue(value, targetType);
    }

    public static bool IsRelationshipType(this Type type) =>
        Helpers.IsRelationshipType(type);

    public static bool IsCollectionOfRelationshipType(this Type type) =>
        Helpers.IsCollectionOfRelationshipType(type);

    public static bool IsPrimitiveOrSimple(this Type type) =>
        type switch
        {
            _ when type.IsPrimitive => true,
            _ when type.IsEnum => true,
            _ when type == typeof(string) => true,
            _ when type.IsValueType => true,
            _ when type == typeof(decimal) => true,
            _ when type == typeof(Model.Point) => true,
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
        _ when Helpers.IsRelationshipType(type) => true,
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
*/
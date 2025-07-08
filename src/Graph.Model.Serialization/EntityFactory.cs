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

namespace Cvoya.Graph.Model.Serialization;

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Represents the serialization/deserialization logic for the graph model.
/// </summary>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
public class EntityFactory(ILoggerFactory? loggerFactory = null)
{
    private readonly ILogger<EntityFactory> _logger = loggerFactory?.CreateLogger<EntityFactory>()
        ?? NullLogger<EntityFactory>.Instance;

    private readonly EntitySerializerRegistry _serializerRegistry = EntitySerializerRegistry.Instance;
    private readonly ConcurrentDictionary<Type, EntitySchema> _schemas = new();

    /// <summary>
    /// Deserializes an <see cref="IEntity"/>" from its serialized form.
    /// </summary>
    /// <param name="entity">The serialized entity information.</param>
    /// <returns>A .NET object graph</returns>
    public object Deserialize(EntityInfo entity)
    {
        // Handle dynamic entities
        if (entity.ActualType.IsAssignableTo(typeof(IDynamicNode)) || entity.ActualType.IsAssignableTo(typeof(IDynamicRelationship)))
        {
            return DeserializeDynamicEntity(entity);
        }

        var serializer = _serializerRegistry.GetSerializer(entity.ActualType)
            ?? throw new GraphException($"No serializer found for type {entity.ActualType}. Ensure it is registered in the EntitySerializerRegistry.");

        return serializer.Deserialize(entity);
    }

    /// <summary>
    /// Deserializes an <see cref="EntityInfo"/> into a .NET object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity to deserialize into, which must implement <see cref="IEntity"/>.</typeparam>
    /// <param name="entity">The <see cref="EntityInfo"/> to deserialize.</param>
    /// <returns>A .NET object graph from the <see cref="EntityInfo"/> representation.</returns>
    public T Deserialize<T>(EntityInfo entity) => (T)Deserialize(entity);

    /// <summary>
    /// Serializes an <see cref="IEntity"/> into an <see cref="EntityInfo"/> representation.
    /// </summary>
    /// <param name="entity">The .NET object to serialize, which must implement <see cref="IEntity"/>.</param>
    /// <returns>An <see cref="EntityInfo"/> representation of the .NET object.</returns>
    public EntityInfo Serialize(IEntity entity)
    {
        return entity switch
        {
            IDynamicNode node => SerializeDynamicEntity(node),
            IDynamicRelationship relationship => SerializeDynamicEntity(relationship),
            _ => SerializeEntityUsingGeneratedSerializer(entity),
        };
    }

    /// <summary>
    /// Checks if the factory can deserialize a given type.
    /// </summary>
    /// <param name="type">The type to check for deserialization capability.</param>
    /// <returns>True if the factory can deserialize the type, otherwise false.</returns>
    public bool CanDeserialize(Type type) =>
        _serializerRegistry.ContainsType(type) ||
        typeof(INode).IsAssignableFrom(type) ||
        typeof(IRelationship).IsAssignableFrom(type) ||
        type == typeof(IDynamicNode) ||
        type == typeof(IDynamicRelationship);

    /// <summary>
    /// Retrieves the schema for a given entity type.
    /// </summary>
    /// <param name="entityType">The type of the entity for which to retrieve the schema.</param>
    /// <returns>An <see cref="EntitySchema"/> representing the schema of the entity type, or null if no serializer is found.</returns>
    /// <exception cref="GraphException"></exception>
    public EntitySchema? GetSchema(Type entityType)
    {
        if (_schemas.TryGetValue(entityType, out var schema))
        {
            return schema;
        }

        var serializer = _serializerRegistry.GetSerializer(entityType)
            ?? null;

        var s = serializer?.GetSchema();
        if (s != null)
        {
            _schemas[entityType] = s;
        }

        return s;
    }

    private EntityInfo SerializeEntityUsingGeneratedSerializer(IEntity entity)
    {
        var serializer = _serializerRegistry.GetSerializer(entity.GetType())
           ?? throw new GraphException($"No serializer found for type {entity.GetType().Name}. Ensure it is registered in the EntitySerializerRegistry.");

        return serializer.Serialize(entity);
    }

    private EntityInfo SerializeDynamicEntity(IEntity entity)
    {
        var simpleProperties = new Dictionary<string, Property>();
        var complexProperties = new Dictionary<string, Property>();

        switch (entity)
        {
            case IDynamicNode node:
                // Add the Id property
                simpleProperties[nameof(IDynamicNode.Id)] = new Property(
                    GetPropertyInfo(typeof(IDynamicNode), nameof(IDynamicNode.Id)),
                    nameof(IDynamicNode.Id),
                    false,
                    new SimpleValue(node.Id, typeof(string)));

                // Add the Labels property
                simpleProperties[nameof(IDynamicNode.Labels)] = new Property(
                    GetPropertyInfo(typeof(IDynamicNode), nameof(IDynamicNode.Labels)),
                    nameof(IDynamicNode.Labels),
                    false,
                    new SimpleCollection(node.Labels.Select(l => new SimpleValue(l, typeof(string))).ToList(), typeof(string)));

                // Process dynamic properties
                ProcessDynamicProperties(node.Properties, simpleProperties, complexProperties);

                return new EntityInfo(
                    typeof(IDynamicNode),
                    node.Labels.FirstOrDefault() ?? "",
                    node.Labels.ToList().AsReadOnly(),
                    simpleProperties,
                    complexProperties);

            case IDynamicRelationship relationship:
                // Add the Id property
                simpleProperties[nameof(IDynamicRelationship.Id)] = new Property(
                    GetPropertyInfo(typeof(IDynamicRelationship), nameof(IDynamicRelationship.Id)),
                    nameof(IDynamicRelationship.Id),
                    false,
                    new SimpleValue(relationship.Id, typeof(string)));

                // Add the Type property
                simpleProperties[nameof(IDynamicRelationship.Type)] = new Property(
                    GetPropertyInfo(typeof(IDynamicRelationship), nameof(IDynamicRelationship.Type)),
                    nameof(IDynamicRelationship.Type),
                    false,
                    new SimpleValue(relationship.Type, typeof(string)));

                // Add the StartNodeId property
                simpleProperties[nameof(IDynamicRelationship.StartNodeId)] = new Property(
                    GetPropertyInfo(typeof(IDynamicRelationship), nameof(IDynamicRelationship.StartNodeId)),
                    nameof(IDynamicRelationship.StartNodeId),
                    false,
                    new SimpleValue(relationship.StartNodeId, typeof(string)));

                // Add the EndNodeId property
                simpleProperties[nameof(IDynamicRelationship.EndNodeId)] = new Property(
                    GetPropertyInfo(typeof(IDynamicRelationship), nameof(IDynamicRelationship.EndNodeId)),
                    nameof(IDynamicRelationship.EndNodeId),
                    false,
                    new SimpleValue(relationship.EndNodeId, typeof(string)));

                // Process dynamic properties
                ProcessDynamicProperties(relationship.Properties, simpleProperties, complexProperties);

                return new EntityInfo(
                    typeof(IDynamicRelationship),
                    relationship.Type,
                    [],
                    simpleProperties,
                    complexProperties);

            default:
                throw new GraphException($"Unsupported dynamic entity type: {entity.GetType().Name}");
        }
    }

    private object DeserializeDynamicEntity(EntityInfo entity)
    {
        switch (entity.ActualType)
        {
            case var t when t.IsAssignableTo(typeof(IDynamicNode)):
                return DeserializeDynamicNode(entity);
            case var t when t.IsAssignableTo(typeof(IDynamicRelationship)):
                return DeserializeDynamicRelationship(entity);
            default:
                throw new GraphException($"Unsupported dynamic entity type: {entity.ActualType.Name}");
        }
    }

    private IDynamicNode DeserializeDynamicNode(EntityInfo entity)
    {
        var properties = new Dictionary<string, object?>();

        // Extract dynamic properties from simple properties
        foreach (var kvp in entity.SimpleProperties)
        {
            if (kvp.Key != nameof(IEntity.Id)
                && kvp.Key != nameof(IDynamicNode.Labels)
                && kvp.Value.Value is SimpleValue simpleValue)
            {
                properties[kvp.Key] = simpleValue.Object;
            }
        }

        // Add complex properties as dictionaries
        foreach (var kvp in entity.ComplexProperties)
        {
            if (kvp.Value.Value is EntityInfo complexEntity)
            {
                // Convert the complex entity's simple properties to a dictionary
                var dict = new Dictionary<string, object?>();
                foreach (var prop in complexEntity.SimpleProperties)
                {
                    if (prop.Value.Value is SimpleValue sv)
                        dict[prop.Key] = sv.Object;
                }
                // Always set to an empty dictionary if no properties
                if (dict.Count == 0)
                    dict = new Dictionary<string, object?>();
                // Debug output
                if (kvp.Key == "address")
                {
                    var keys = dict != null ? string.Join(", ", dict.Keys) : "<null>";
                    System.Diagnostics.Debug.WriteLine($"[DeserializeDynamicNode] address property type: {dict?.GetType().FullName}, keys: {keys}");
                }
                properties[kvp.Key] = dict;
            }
            else if (kvp.Value.Value is EntityCollection collection)
            {
                // Handle collections of complex entities
                var list = new List<Dictionary<string, object?>>();
                foreach (var item in collection.Entities)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in item.SimpleProperties)
                    {
                        if (prop.Value.Value is SimpleValue sv)
                            dict[prop.Key] = sv.Object;
                    }
                    list.Add(dict);
                }
                properties[kvp.Key] = list;
            }
        }

        // Prefer ActualLabels if present
        List<string> labels;
        if (entity.ActualLabels != null && entity.ActualLabels.Count > 0)
        {
            labels = entity.ActualLabels.ToList();
        }
        else if (!string.IsNullOrEmpty(entity.Label))
        {
            labels = entity.Label.Split(':').ToList();
        }
        else
        {
            labels = new List<string>();
        }

        return new DynamicNode
        {
            Id = entity.SimpleProperties.TryGetValue(nameof(IEntity.Id), out var idProp) && idProp.Value is SimpleValue idVal ? idVal.Object?.ToString() ?? string.Empty : string.Empty,
            Labels = labels,
            Properties = properties
        };
    }

    private IDynamicRelationship DeserializeDynamicRelationship(EntityInfo entity)
    {
        var properties = new Dictionary<string, object?>();

        // Extract dynamic properties from simple properties
        foreach (var kvp in entity.SimpleProperties)
        {
            if (kvp.Key != nameof(IEntity.Id)
                && kvp.Key != nameof(IDynamicRelationship.Type)
                && kvp.Key != nameof(IDynamicRelationship.StartNodeId)
                && kvp.Key != nameof(IDynamicRelationship.EndNodeId)
                && kvp.Value.Value is SimpleValue simpleValue)
            {
                properties[kvp.Key] = simpleValue.Object;
            }
        }

        // Extract required properties
        string id = "";
        string type = "";
        string startNodeId = "";
        string endNodeId = "";

        if (entity.SimpleProperties.TryGetValue(nameof(IEntity.Id), out var idProperty) &&
            idProperty.Value is SimpleValue idValue)
        {
            id = idValue.Object?.ToString() ?? "";
        }

        // Set type from entity.Label (authoritative from Neo4j)
        if (!string.IsNullOrEmpty(entity.Label))
        {
            type = entity.Label;
        }
        else if (entity.SimpleProperties.TryGetValue(nameof(IDynamicRelationship.Type), out var typeProperty) &&
            typeProperty.Value is SimpleValue typeValue)
        {
            type = typeValue.Object?.ToString() ?? "";
        }

        if (entity.SimpleProperties.TryGetValue(nameof(IDynamicRelationship.StartNodeId), out var startNodeIdProperty) &&
            startNodeIdProperty.Value is SimpleValue startNodeIdValue)
        {
            startNodeId = startNodeIdValue.Object?.ToString() ?? "";
        }

        if (entity.SimpleProperties.TryGetValue(nameof(IDynamicRelationship.EndNodeId), out var endNodeIdProperty) &&
            endNodeIdProperty.Value is SimpleValue endNodeIdValue)
        {
            endNodeId = endNodeIdValue.Object?.ToString() ?? "";
        }

        // Process complex properties
        ProcessComplexProperties(entity.ComplexProperties, properties);

        return new DynamicRelationship(startNodeId, endNodeId, type, properties);
    }

    private void ProcessComplexProperties(
        IDictionary<string, Property> complexProperties,
        Dictionary<string, object?> properties)
    {
        foreach (var kvp in complexProperties)
        {
            var propertyName = kvp.Key;
            var property = kvp.Value;

            if (property.Value == null)
            {
                properties[propertyName] = null;
                continue;
            }

            if (property.Value is EntityInfo entityInfo)
            {
                // Single complex object
                var complexObject = DeserializeComplexObject(entityInfo);
                properties[propertyName] = complexObject;
            }
            else if (property.Value is EntityCollection entityCollection)
            {
                // Collection of complex objects
                var complexObjects = new List<object>();
                foreach (var itemEntityInfo in entityCollection.Entities)
                {
                    var complexObject = DeserializeComplexObject(itemEntityInfo);
                    complexObjects.Add(complexObject);
                }
                properties[propertyName] = complexObjects;
            }
        }
    }

    private object DeserializeComplexObject(EntityInfo entityInfo)
    {
        // Create an instance of the target type
        var targetType = entityInfo.ActualType;
        var instance = Activator.CreateInstance(targetType);

        if (instance == null)
        {
            throw new GraphException($"Failed to create instance of type {targetType.Name}");
        }

        // Set simple properties
        foreach (var kvp in entityInfo.SimpleProperties)
        {
            var propertyName = kvp.Key;
            var property = kvp.Value;

            if (property.Value is SimpleValue simpleValue)
            {
                SetPropertyValue(instance, propertyName, simpleValue.Object);
            }
            else if (property.Value is SimpleCollection simpleCollection)
            {
                var collection = CreateCollection(simpleCollection.ElementType, simpleCollection.Values.Select(v => v.Object));
                SetPropertyValue(instance, propertyName, collection);
            }
        }

        // Set complex properties
        foreach (var kvp in entityInfo.ComplexProperties)
        {
            var propertyName = kvp.Key;
            var property = kvp.Value;

            if (property.Value is EntityInfo complexEntityInfo)
            {
                // Single complex object
                var complexObject = DeserializeComplexObject(complexEntityInfo);
                SetPropertyValue(instance, propertyName, complexObject);
            }
            else if (property.Value is EntityCollection entityCollection)
            {
                // Collection of complex objects
                var complexObjects = new List<object>();
                foreach (var itemEntityInfo in entityCollection.Entities)
                {
                    var complexObject = DeserializeComplexObject(itemEntityInfo);
                    complexObjects.Add(complexObject);
                }
                SetPropertyValue(instance, propertyName, complexObjects);
            }
        }

        return instance;
    }

    private static void SetPropertyValue(object instance, string propertyName, object? value)
    {
        var propertyInfo = instance.GetType().GetProperty(propertyName);
        if (propertyInfo?.CanWrite == true)
        {
            // Try to convert the value to the target type if needed
            var convertedValue = ConvertValue(value, propertyInfo.PropertyType);
            propertyInfo.SetValue(instance, convertedValue);
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            return Convert.ChangeType(value, underlyingType);
        }

        // Handle basic type conversions
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // If conversion fails, return the original value
            return value;
        }
    }

    private static object CreateCollection(Type elementType, IEnumerable<object?> values)
    {
        // Create a List<T> for the collection
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var value in values)
        {
            var convertedValue = ConvertValue(value, elementType);
            list.Add(convertedValue!);
        }

        return list;
    }

    private void ProcessDynamicProperties(
        IReadOnlyDictionary<string, object?> properties,
        Dictionary<string, Property> simpleProperties,
        Dictionary<string, Property> complexProperties)
    {
        foreach (var kvp in properties)
        {
            var propertyName = kvp.Key;
            var propertyValue = kvp.Value;

            if (propertyValue == null)
            {
                // Null values are treated as simple properties
                simpleProperties[propertyName] = new Property(
                    GetPropertyInfo(typeof(object), propertyName),
                    propertyName,
                    true,
                    new SimpleValue(null!, typeof(object)));
                continue;
            }

            var valueType = propertyValue.GetType();

            if (GraphDataModel.IsSimple(valueType))
            {
                // Simple property
                simpleProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    new SimpleValue(propertyValue, valueType));
            }
            else if (GraphDataModel.IsCollectionOfSimple(valueType))
            {
                // Collection of simple values
                var collection = (IEnumerable)propertyValue;
                var simpleValues = new List<SimpleValue>();
                var elementType = GetElementType(valueType);

                foreach (var item in collection)
                {
                    simpleValues.Add(new SimpleValue(item ?? (object)"", item?.GetType() ?? elementType));
                }

                simpleProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    new SimpleCollection(simpleValues, elementType));
            }
            else if (GraphDataModel.IsCollectionOfComplex(valueType))
            {
                // Collection of complex values
                var collection = (IEnumerable)propertyValue;
                var complexValues = new List<EntityInfo>();
                var elementType = GetElementType(valueType);

                foreach (var item in collection)
                {
                    if (item != null)
                    {
                        // Recursively serialize complex objects
                        var itemEntityInfo = SerializeComplexObject(item, elementType);
                        complexValues.Add(itemEntityInfo);
                    }
                }

                complexProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    new EntityCollection(elementType, complexValues));
            }
            else if (valueType.IsAssignableTo(typeof(IDictionary<string, object?>)) || valueType.IsAssignableTo(typeof(IDictionary<string, object>)))
            {
                // Convert dictionary to EntityInfo with simple properties for each key-value pair
                var dict = (IDictionary<string, object?>)propertyValue;
                var dictSimpleProperties = new Dictionary<string, Property>();
                var dictComplexProperties = new Dictionary<string, Property>();

                foreach (var dictKvp in dict)
                {
                    var key = dictKvp.Key;
                    var value = dictKvp.Value;

                    if (value == null)
                    {
                        dictSimpleProperties[key] = new Property(
                            GetPropertyInfo(typeof(object), key),
                            key,
                            true,
                            new SimpleValue(null!, typeof(object)));
                    }
                    else if (GraphDataModel.IsSimple(value.GetType()))
                    {
                        dictSimpleProperties[key] = new Property(
                            GetPropertyInfo(value.GetType(), key),
                            key,
                            false,
                            new SimpleValue(value, value.GetType()));
                    }
                    else
                    {
                        // Recursively handle nested complex objects
                        var nestedComplexEntityInfo = SerializeComplexObject(value, value.GetType());
                        dictComplexProperties[key] = new Property(
                            GetPropertyInfo(value.GetType(), key),
                            key,
                            false,
                            nestedComplexEntityInfo);
                    }
                }

                var dictEntityInfo = new EntityInfo(
                    valueType,
                    "Dictionary",
                    new List<string>(),
                    dictSimpleProperties,
                    dictComplexProperties);

                complexProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    dictEntityInfo);
            }
            else
            {
                // Complex property - recursively serialize
                var complexEntityInfo = SerializeComplexObject(propertyValue, valueType);
                complexProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    complexEntityInfo);
            }
        }
    }

    private EntityInfo SerializeComplexObject(object obj, Type objectType)
    {
        var simpleProperties = new Dictionary<string, Property>();
        var complexProperties = new Dictionary<string, Property>();

        // Get all public properties of the object
        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var propertyInfo in properties)
        {
            if (!propertyInfo.CanRead)
                continue;

            var propertyName = propertyInfo.Name;
            var propertyValue = propertyInfo.GetValue(obj);
            var propertyType = propertyInfo.PropertyType;

            if (propertyValue == null)
            {
                // Null values are treated as simple properties
                simpleProperties[propertyName] = new Property(
                    propertyInfo,
                    propertyName,
                    true,
                    new SimpleValue(null!, typeof(object)));
                continue;
            }

            if (GraphDataModel.IsSimple(propertyType))
            {
                // Simple property
                simpleProperties[propertyName] = new Property(
                    propertyInfo,
                    propertyName,
                    false,
                    new SimpleValue(propertyValue, propertyType));
            }
            else if (GraphDataModel.IsCollectionOfSimple(propertyType))
            {
                // Collection of simple values
                var collection = (IEnumerable)propertyValue;
                var simpleValues = new List<SimpleValue>();
                var elementType = GetElementType(propertyType);

                foreach (var item in collection)
                {
                    simpleValues.Add(new SimpleValue(item ?? (object)"", item?.GetType() ?? elementType));
                }

                simpleProperties[propertyName] = new Property(
                    propertyInfo,
                    propertyName,
                    false,
                    new SimpleCollection(simpleValues, elementType));
            }
            else if (GraphDataModel.IsCollectionOfComplex(propertyType))
            {
                // Collection of complex values
                var collection = (IEnumerable)propertyValue;
                var complexValues = new List<EntityInfo>();
                var elementType = GetElementType(propertyType);

                foreach (var item in collection)
                {
                    if (item != null)
                    {
                        // Recursively serialize complex objects
                        var itemEntityInfo = SerializeComplexObject(item, elementType);
                        complexValues.Add(itemEntityInfo);
                    }
                }

                complexProperties[propertyName] = new Property(
                    propertyInfo,
                    propertyName,
                    false,
                    new EntityCollection(elementType, complexValues));
            }
            else
            {
                // Complex property - recursively serialize
                var complexEntityInfo = SerializeComplexObject(propertyValue, propertyType);
                complexProperties[propertyName] = new Property(
                    propertyInfo,
                    propertyName,
                    false,
                    complexEntityInfo);
            }
        }

        return new EntityInfo(
            objectType,
            objectType.Name,
            [],
            simpleProperties,
            complexProperties);
    }

    private static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType()!;
        }

        if (collectionType.IsGenericType)
        {
            var genericArguments = collectionType.GetGenericArguments();
            if (genericArguments.Length > 0)
            {
                return genericArguments[0];
            }
        }

        // Fallback to object if we can't determine the element type
        return typeof(object);
    }

    private static PropertyInfo GetPropertyInfo(Type type, string propertyName)
    {
        return type.GetProperty(propertyName) ??
               typeof(object).GetProperty("ToString")!; // Fallback
    }
}
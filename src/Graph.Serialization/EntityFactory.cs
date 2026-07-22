// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

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
        if (entity.ActualType.IsAssignableTo(typeof(DynamicNode)) || entity.ActualType.IsAssignableTo(typeof(DynamicRelationship)))
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
            DynamicNode node => SerializeDynamicEntity(node),
            DynamicRelationship relationship => SerializeDynamicEntity(relationship),
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
        type == typeof(DynamicNode) ||
        type == typeof(DynamicRelationship);

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

    /// <summary>Serializes one typed complex-property value using the registered runtime serializer.</summary>
    internal Property SerializeComplexProperty(
        PropertyInfo propertyInfo,
        string storageName,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageName);

        Serialized? serializedValue;
        if (value is null)
        {
            serializedValue = null;
        }
        else if (GraphDataModel.IsCollectionOfComplex(propertyInfo.PropertyType))
        {
            var elementType = GetElementType(propertyInfo.PropertyType);
            var entities = new List<EntityInfo>();
            var index = 0;
            foreach (var item in (IEnumerable)value)
            {
                if (item is null)
                {
                    throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                        storageName,
                        elementType,
                        index,
                        actualType: null);
                }

                var actualType = item.GetType();
                if (!elementType.IsAssignableFrom(actualType))
                {
                    throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                        storageName,
                        elementType,
                        index,
                        actualType);
                }

                entities.Add(SerializeRegisteredComplexValue(
                    item,
                    actualType,
                    elementType,
                    allowDeclaredTypeFallback: true));
                index++;
            }

            serializedValue = new EntityCollection(elementType, entities);
        }
        else
        {
            var actualType = value.GetType();
            if (!propertyInfo.PropertyType.IsAssignableFrom(actualType))
            {
                throw new GraphException(
                    $"Complex property '{storageName}' expects '{propertyInfo.PropertyType}', " +
                    $"but the assigned value has runtime type '{actualType}'.");
            }

            serializedValue = SerializeRegisteredComplexValue(
                value,
                actualType,
                propertyInfo.PropertyType,
                allowDeclaredTypeFallback: false);
        }

        return new Property(
            propertyInfo,
            storageName,
            value is null || !propertyInfo.PropertyType.IsValueType ||
                Nullable.GetUnderlyingType(propertyInfo.PropertyType) is not null,
            serializedValue,
            GraphDataModel.GetComplexPropertyRelationshipType(propertyInfo));
    }

    /// <summary>Serializes one dynamic value that has already been classified as complex.</summary>
    internal Property SerializeDynamicComplexProperty(string storageName, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageName);
        ArgumentNullException.ThrowIfNull(value);

        var simpleProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var complexProperties = new Dictionary<string, Property>(StringComparer.Ordinal);
        var visited = new HashSet<object>(GraphDataModel.ReferenceEqualityComparer.Instance);
        ClassifyDynamicValue(storageName, value, simpleProperties, complexProperties, visited);
        if (!complexProperties.TryGetValue(storageName, out var property))
        {
            throw new GraphQueryTranslationException(
                $"The dynamic value for '{storageName}' is not a serializable complex property value.");
        }

        return property;
    }

    private EntityInfo SerializeRegisteredComplexValue(
        object value,
        Type actualType,
        Type declaredType,
        bool allowDeclaredTypeFallback)
    {
        var serializer = _serializerRegistry.GetSerializer(actualType)
            ?? (allowDeclaredTypeFallback ? _serializerRegistry.GetSerializer(declaredType) : null)
            ?? throw new GraphException(
                $"No serializer found for complex property type '{actualType}'. " +
                "Ensure the type is included in graph serialization code generation.");
        return serializer.Serialize(value);
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
            case DynamicNode node:
                // Add the Labels property
                simpleProperties[nameof(DynamicNode.Labels)] = new Property(
                    GetPropertyInfo(typeof(DynamicNode), nameof(DynamicNode.Labels)),
                    nameof(DynamicNode.Labels),
                    false,
                    new SimpleCollection(node.Labels.Select(l => new SimpleValue(l, typeof(string))).ToList(), typeof(string)));

                // Process dynamic properties
                ProcessDynamicProperties(node.Properties, simpleProperties, complexProperties);

                return new EntityInfo(
                    typeof(DynamicNode),
                    node.Labels.Count == 0 ? "" : node.Labels[0],
                    node.Labels.ToList().AsReadOnly(),
                    simpleProperties,
                    complexProperties);

            case DynamicRelationship relationship:
                // Add the Type property
                simpleProperties[nameof(DynamicRelationship.Type)] = new Property(
                    GetPropertyInfo(typeof(DynamicRelationship), nameof(DynamicRelationship.Type)),
                    nameof(DynamicRelationship.Type),
                    false,
                    new SimpleValue(relationship.Type, typeof(string)));

                // Process dynamic properties
                ProcessDynamicProperties(relationship.Properties, simpleProperties, complexProperties);

                return new EntityInfo(
                    typeof(DynamicRelationship),
                    relationship.Type,
                    [],
                    simpleProperties,
                    complexProperties);

            default:
                throw new GraphException($"Unsupported dynamic entity type: {entity.GetType().Name}");
        }
    }

    private static object DeserializeDynamicEntity(EntityInfo entity)
    {
        var actualType = entity.ActualType;

        if (actualType.IsAssignableTo(typeof(DynamicNode)))
        {
            return DeserializeDynamicNode(entity);
        }

        if (actualType.IsAssignableTo(typeof(DynamicRelationship)))
        {
            return DeserializeDynamicRelationship(entity);
        }

        throw new GraphException($"Unsupported dynamic entity type: {actualType.Name}");
    }

    private static DynamicNode DeserializeDynamicNode(EntityInfo entity)
    {
        var properties = new Dictionary<string, object?>();

        ExtractDynamicSimpleProperties(
            entity.SimpleProperties,
            properties,
            nameof(DynamicNode.Labels));

        // Materialize complex properties into the canonical dynamic shape shared with relationships.
        AttachDynamicComplexProperties(entity.ComplexProperties, properties);

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
            Labels = labels,
            Properties = properties
        };
    }

    private static DynamicRelationship DeserializeDynamicRelationship(EntityInfo entity)
    {
        var properties = new Dictionary<string, object?>();

        ExtractDynamicSimpleProperties(
            entity.SimpleProperties,
            properties,
            nameof(DynamicRelationship.Type));

        string type = "";

        // Set type from entity.Label (authoritative from the serialized entity)
        if (!string.IsNullOrEmpty(entity.Label))
        {
            type = entity.Label;
        }
        else if (entity.SimpleProperties.TryGetValue(nameof(DynamicRelationship.Type), out var typeProperty) &&
            typeProperty.Value is SimpleValue typeValue)
        {
            type = typeValue.Object?.ToString() ?? "";
        }

        // Materialize complex properties into the canonical dynamic shape shared with nodes.
        AttachDynamicComplexProperties(entity.ComplexProperties, properties);

        return new DynamicRelationship(type, properties);
    }

    private static void ExtractDynamicSimpleProperties(
        IDictionary<string, Property> simpleProperties,
        Dictionary<string, object?> properties,
        params string[] reservedPropertyNames)
    {
        var reserved = reservedPropertyNames.ToHashSet(StringComparer.Ordinal);
        foreach (var (propertyName, property) in simpleProperties)
        {
            if (reserved.Contains(propertyName))
            {
                continue;
            }

            properties[propertyName] = property.Value switch
            {
                // Generated serializers represent a null property as a Property with a null Value.
                null => null,
                SimpleValue simpleValue => simpleValue.Object,
                SimpleCollection simpleCollection => CreateDynamicSimpleCollection(propertyName, simpleCollection),
                _ => throw new GraphException(
                    $"Dynamic property '{propertyName}' has unsupported serialized value type " +
                    $"'{property.Value.GetType().Name}'."),
            };
        }
    }

    private static object CreateDynamicSimpleCollection(string propertyName, SimpleCollection collection)
    {
        try
        {
            var listType = typeof(List<>).MakeGenericType(collection.ElementType);
            var values = (IList)(Activator.CreateInstance(listType)
                ?? throw new GraphException($"Failed to create collection type '{listType}' for dynamic property '{propertyName}'."));

            foreach (var simpleValue in collection.Values)
            {
                if (simpleValue is null)
                {
                    throw new GraphException($"Dynamic collection property '{propertyName}' contains a malformed null item.");
                }

                // GraphValueConverter.ConvertTo would coerce null to default(T) for non-nullable
                // value types, silently inventing an element; fail explicitly instead.
                if (simpleValue.Object is null &&
                    collection.ElementType.IsValueType &&
                    Nullable.GetUnderlyingType(collection.ElementType) is null)
                {
                    throw new GraphException(
                        $"Dynamic collection property '{propertyName}' contains a null element, but its element " +
                        $"type '{collection.ElementType}' cannot represent null.");
                }

                values.Add(Results.GraphValueConverter.ConvertTo(simpleValue.Object, collection.ElementType));
            }

            return values;
        }
        catch (GraphException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or FormatException or OverflowException)
        {
            throw new GraphException(
                $"Failed to deserialize dynamic collection property '{propertyName}' as elements of type " +
                $"'{collection.ElementType}'.",
                exception);
        }
    }

    /// <summary>
    /// Materializes serialized complex properties into a dynamic entity's property bag using the
    /// canonical dynamic shape. Nodes and relationships share this path so the same stored complex
    /// value round-trips to the same shape regardless of which owns it.
    /// </summary>
    private static void AttachDynamicComplexProperties(
        IDictionary<string, Property> complexProperties,
        Dictionary<string, object?> properties)
    {
        foreach (var (propertyName, property) in complexProperties)
        {
            properties[propertyName] = MaterializeDynamicComplexValue(propertyName, property.Value);
        }
    }

    /// <summary>
    /// Materializes a single serialized complex value into the canonical dynamic shape: a
    /// <see cref="Dictionary{TKey, TValue}"/> for a single complex value, or a <see cref="List{T}"/>
    /// of such dictionaries for a collection of complex values.
    /// </summary>
    private static object? MaterializeDynamicComplexValue(string propertyName, Serialized? value)
    {
        return value switch
        {
            null => null,
            EntityInfo entityInfo => MaterializeDynamicComplexEntity(entityInfo),
            EntityCollection collection => MaterializeDynamicComplexCollection(propertyName, collection),
            _ => throw new GraphException(
                $"Dynamic complex property '{propertyName}' has unsupported serialized value type " +
                $"'{value.GetType().Name}'."),
        };
    }

    /// <summary>
    /// Materializes one serialized complex entity into a <see cref="Dictionary{TKey, TValue}"/>,
    /// keyed by the stored (physical) property labels. Nested simple collections are reconstructed
    /// as <see cref="List{T}"/> values and nested complex values recurse through
    /// <see cref="MaterializeDynamicComplexValue"/>, so the full object graph survives the round trip.
    /// </summary>
    private static Dictionary<string, object?> MaterializeDynamicComplexEntity(EntityInfo entityInfo)
    {
        var dictionary = new Dictionary<string, object?>();

        foreach (var (memberName, property) in entityInfo.SimpleProperties)
        {
            dictionary[memberName] = property.Value switch
            {
                // A null-valued simple property is stored as a SimpleValue whose Object is null.
                null => null,
                SimpleValue simpleValue => simpleValue.Object,
                SimpleCollection simpleCollection => CreateDynamicSimpleCollection(memberName, simpleCollection),
                _ => throw new GraphException(
                    $"Dynamic complex property member '{memberName}' has unsupported serialized value " +
                    $"type '{property.Value.GetType().Name}'."),
            };
        }

        foreach (var (memberName, property) in entityInfo.ComplexProperties)
        {
            dictionary[memberName] = MaterializeDynamicComplexValue(memberName, property.Value);
        }

        return dictionary;
    }

    private static List<Dictionary<string, object?>> MaterializeDynamicComplexCollection(
        string propertyName,
        EntityCollection collection)
    {
        var values = new List<Dictionary<string, object?>>(collection.Entities.Count);
        var index = 0;
        foreach (var entity in collection.Entities)
        {
            if (entity is null)
            {
                throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                    propertyName,
                    collection.Type,
                    index,
                    actualType: null);
            }

            if (collection.Type != typeof(object) &&
                !collection.Type.IsAssignableFrom(entity.ActualType))
            {
                throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                    propertyName,
                    collection.Type,
                    index,
                    entity.ActualType);
            }

            values.Add(MaterializeDynamicComplexEntity(entity));
            index++;
        }

        return values;
    }

    private void ProcessDynamicProperties(
        IReadOnlyDictionary<string, object?> properties,
        Dictionary<string, Property> simpleProperties,
        Dictionary<string, Property> complexProperties)
    {
        var visited = new HashSet<object>(Cvoya.Graph.GraphDataModel.ReferenceEqualityComparer.Instance);
        ProcessDynamicProperties(properties, simpleProperties, complexProperties, visited);
    }

    private void ProcessDynamicProperties(
        IReadOnlyDictionary<string, object?> properties,
        Dictionary<string, Property> simpleProperties,
        Dictionary<string, Property> complexProperties,
        HashSet<object> visited)
    {
        _logger.LogDebugEntityFactory533(properties.Count);

        foreach (var (propertyName, propertyValue) in properties)
        {
            _logger.LogDebugEntityFactory540(propertyName, propertyValue, propertyValue?.GetType().Name ?? "null");
            ClassifyDynamicValue(propertyName, propertyValue, simpleProperties, complexProperties, visited);
        }
    }

    /// <summary>
    /// Classifies one dynamic value - a top-level property or a dictionary entry - and records it as
    /// a simple or complex <see cref="Property"/>. Dictionary entries recurse through this same
    /// classification, so a simple collection, complex collection, or nested dictionary nested inside
    /// a dictionary value is preserved as element data instead of being reflected over as an opaque
    /// object (which would serialize a collection's <c>Length</c>/<c>Rank</c>/... instead of its items).
    /// </summary>
    private void ClassifyDynamicValue(
        string propertyName,
        object? propertyValue,
        Dictionary<string, Property> simpleProperties,
        Dictionary<string, Property> complexProperties,
        HashSet<object> visited)
    {
        if (propertyValue == null)
        {
            // Null values are treated as simple properties
            simpleProperties[propertyName] = new Property(
                GetPropertyInfo(typeof(object), propertyName),
                propertyName,
                true,
                new SimpleValue(null!, typeof(object)));
            return;
        }

        var valueType = propertyValue.GetType();

        // Handle JsonValueOfElement by extracting the actual value
        if (propertyValue is System.Text.Json.JsonElement jsonElement)
        {
            propertyValue = jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Number => jsonElement.GetDecimal(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                _ => propertyValue
            };
            valueType = propertyValue?.GetType() ?? typeof(object);
        }

        if (GraphDataModel.IsSimple(valueType))
        {
            // Simple property
            _logger.LogDebugEntityFactory574(propertyName);
            simpleProperties[propertyName] = new Property(
                GetPropertyInfo(valueType, propertyName),
                propertyName,
                propertyValue == null,
                new SimpleValue(propertyValue ?? (object)"", valueType));
        }
        else if (GraphDataModel.IsCollectionOfSimple(valueType))
        {
            // Collection of simple values
            var collection = (IEnumerable)propertyValue!;
            var simpleValues = new List<SimpleValue>();
            var elementType = GetElementType(valueType);

            foreach (var item in collection)
            {
                simpleValues.Add(new SimpleValue(item!, elementType));
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
            var collection = (IEnumerable)propertyValue!;
            var complexValues = new List<EntityInfo>();
            var elementType = GetElementType(valueType);

            var index = 0;
            foreach (var item in collection)
            {
                if (item is null)
                {
                    throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                        propertyName,
                        elementType,
                        index,
                        actualType: null);
                }

                var actualType = item.GetType();
                if (!elementType.IsAssignableFrom(actualType))
                {
                    throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                        propertyName,
                        elementType,
                        index,
                        actualType);
                }

                // Recursively serialize complex objects with cycle detection
                var itemEntityInfo = SerializeComplexObject(item, actualType, visited);
                complexValues.Add(itemEntityInfo);
                index++;
            }

            complexProperties[propertyName] = new Property(
                GetPropertyInfo(valueType, propertyName),
                propertyName,
                false,
                new EntityCollection(elementType, complexValues),
                RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(propertyName));
        }
        else if (IsCollectionOfDynamicDictionaries(valueType))
        {
            if (!visited.Add(propertyValue!))
            {
                throw new GraphException(
                    $"Reference cycle detected while serializing dynamic property '{propertyName}'.");
            }

            try
            {
                var complexValues = new List<EntityInfo>();
                var elementType = GetElementType(valueType);

                var index = 0;
                foreach (var item in (IEnumerable)propertyValue!)
                {
                    if (item is null)
                    {
                        throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                            propertyName,
                            elementType,
                            index,
                            actualType: null);
                    }

                    if (!elementType.IsAssignableFrom(item.GetType()))
                    {
                        throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                            propertyName,
                            elementType,
                            index,
                            item.GetType());
                    }

                    complexValues.Add(SerializeDynamicDictionary(item, item.GetType(), propertyName, visited));
                    index++;
                }

                complexProperties[propertyName] = new Property(
                    GetPropertyInfo(valueType, propertyName),
                    propertyName,
                    false,
                    new EntityCollection(elementType, complexValues),
                    RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(propertyName));
            }
            finally
            {
                visited.Remove(propertyValue!);
            }
        }
        else if (IsDynamicDictionary(valueType))
        {
            var dictEntityInfo = SerializeDynamicDictionary(propertyValue!, valueType, propertyName, visited);

            complexProperties[propertyName] = new Property(
                GetPropertyInfo(valueType, propertyName),
                propertyName,
                false,
                dictEntityInfo,
                RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(propertyName));
        }
        else
        {
            // Complex property - recursively serialize with cycle detection
            var complexEntityInfo = SerializeComplexObject(propertyValue!, valueType, visited);
            complexProperties[propertyName] = new Property(
                GetPropertyInfo(valueType, propertyName),
                propertyName,
                false,
                complexEntityInfo,
                RelationshipType: GraphDataModel.PropertyNameToRelationshipTypeName(propertyName));
        }
    }

    private void ClassifyDynamicDictionaryEntries(
        object dictionaryValue,
        Dictionary<string, Property> simpleProperties,
        Dictionary<string, Property> complexProperties,
        HashSet<object> visited)
    {
        foreach (var (key, value) in (IEnumerable<KeyValuePair<string, object?>>)dictionaryValue)
        {
            ClassifyDynamicValue(key, value, simpleProperties, complexProperties, visited);
        }
    }

    private EntityInfo SerializeDynamicDictionary(
        object dictionaryValue,
        Type dictionaryType,
        string propertyName,
        HashSet<object> visited)
    {
        if (!visited.Add(dictionaryValue))
        {
            throw new GraphException(
                $"Reference cycle detected while serializing dynamic property '{propertyName}'.");
        }

        try
        {
            var simpleProperties = new Dictionary<string, Property>();
            var complexProperties = new Dictionary<string, Property>();
            ClassifyDynamicDictionaryEntries(dictionaryValue, simpleProperties, complexProperties, visited);

            return new EntityInfo(
                dictionaryType,
                "Dictionary",
                [],
                simpleProperties,
                complexProperties);
        }
        finally
        {
            visited.Remove(dictionaryValue);
        }
    }

    private static bool IsCollectionOfDynamicDictionaries(Type type) =>
        type != typeof(string) &&
        typeof(IEnumerable).IsAssignableFrom(type) &&
        !GraphDataModel.IsDictionary(type) &&
        IsDynamicDictionary(GetElementType(type));

    private static bool IsDynamicDictionary(Type type) =>
        GraphDataModel.IsDictionary(type) &&
        typeof(IEnumerable<KeyValuePair<string, object?>>).IsAssignableFrom(type);

    private static EntityInfo SerializeComplexObject(object obj, Type objectType)
    {
        // Use a thread-safe approach with cycle detection during serialization
        var visited = new HashSet<object>(Cvoya.Graph.GraphDataModel.ReferenceEqualityComparer.Instance);
        return SerializeComplexObject(obj, objectType, visited);
    }

    private static EntityInfo SerializeComplexObject(object obj, Type objectType, HashSet<object> visited)
    {
        // Check for circular references during serialization to prevent stack overflow
        if (visited.Contains(obj))
        {
            // Return a minimal EntityInfo to break the cycle
            return new EntityInfo(
                objectType,
                objectType.Name,
                [],
                new Dictionary<string, Property>(),
                new Dictionary<string, Property>());
        }

        visited.Add(obj);

        try
        {
            var simpleProperties = new Dictionary<string, Property>();
            var complexProperties = new Dictionary<string, Property>();

            // Get all public properties of the object - this is thread-safe
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var propertyInfo in properties)
            {
                if (!propertyInfo.CanRead)
                    continue;

                // Skip properties with parameters (like indexers)
                if (propertyInfo.GetIndexParameters().Length > 0)
                    continue;

                var propertyName = Labels.GetLabelFromProperty(propertyInfo);
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
                        simpleValues.Add(new SimpleValue(item!, elementType));
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

                    var index = 0;
                    foreach (var item in collection)
                    {
                        if (item is null)
                        {
                            throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                                propertyName,
                                elementType,
                                index,
                                actualType: null);
                        }

                        var actualType = item.GetType();
                        if (!elementType.IsAssignableFrom(actualType))
                        {
                            throw Results.GraphValueConverter.CreateInvalidComplexCollectionElementException(
                                propertyName,
                                elementType,
                                index,
                                actualType);
                        }

                        // Recursively serialize complex objects with cycle detection
                        var itemEntityInfo = SerializeComplexObject(item, actualType, visited);
                        complexValues.Add(itemEntityInfo);
                        index++;
                    }

                    complexProperties[propertyName] = new Property(
                        propertyInfo,
                        propertyName,
                        false,
                        new EntityCollection(elementType, complexValues),
                        RelationshipType: GraphDataModel.GetComplexPropertyRelationshipType(propertyInfo));
                }
                else
                {
                    // Complex property - recursively serialize with cycle detection
                    var complexEntityInfo = SerializeComplexObject(propertyValue, propertyType, visited);
                    complexProperties[propertyName] = new Property(
                        propertyInfo,
                        propertyName,
                        false,
                        complexEntityInfo,
                        RelationshipType: GraphDataModel.GetComplexPropertyRelationshipType(propertyInfo));
                }
            }

            return new EntityInfo(
                objectType,
                SanitizeTypeNameForStorage(objectType),
                [],
                simpleProperties,
                complexProperties);
        }
        finally
        {
            visited.Remove(obj);
        }
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

    private static string SanitizeTypeNameForStorage(Type type)
    {
        // Handle generic types like Nullable<T> -> NullableT
        if (type.IsGenericType)
        {
            var name = type.Name;
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex > 0)
            {
                name = name.Substring(0, backtickIndex);
            }

            // Add generic type parameters
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                name += string.Join("", genericArgs.Select(SanitizeTypeNameForStorage));
            }

            return name;
        }

        // For non-generic types, just return the name
        return type.Name;
    }

    private static PropertyInfo GetPropertyInfo(Type type, string propertyName)
    {
        return type.GetProperty(propertyName) ??
               typeof(object).GetProperty("ToString")!; // Fallback
    }
}

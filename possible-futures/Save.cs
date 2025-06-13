/// <summary>
/// Converts Neo4j query results to intermediate Entity representations that can be deserialized.
/// </summary>
/// <param name="record">The Neo4j record containing the main node and related nodes</param>
/// <param name="mainNodeKey">The key for the main node in the record (usually the node alias)</param>
/// <param name="relatedNodesKey">The key for the related nodes collection (usually "relatedNodes")</param>
/// <returns>An Entity representation that can be passed to a serializer</returns>
public Entity ConvertToIntermediateRepresentation(IRecord record, string mainNodeKey, string relatedNodesKey = "relatedNodes")
{
    // Get the main node from the record
    var mainNode = record[mainNodeKey].As<global::Neo4j.Driver.INode>();

    // Get the related nodes (complex properties) if they exist
    var relatedNodes = record.ContainsKey(relatedNodesKey) && (record[relatedNodesKey] is not null)
        ? record[relatedNodesKey].As<IList<IDictionary<string, object>>>()
        : new List<IDictionary<string, object>>();

    return ConvertNodeToEntity(mainNode, relatedNodes);
}

/// <summary>
/// Converts a single Neo4j node to an Entity representation.
/// </summary>
public Entity ConvertNodeToEntity(global::Neo4j.Driver.IEntity entity, IList<IDictionary<string, object>>? relatedNodes = null)
{
    // Find the schema for this node based on its labels
    var label = entity is global::Neo4j.Driver.INode node
        ? node.Labels.FirstOrDefault()
        ?? throw new ArgumentException("Node must have at least one label", nameof(entity))
        if (useMostDerivedType)
    {
        // Use the label from the Neo4j node to find the most derived type
        var label = entity is global::Neo4j.Driver.INode node
            ? node.Labels.FirstOrDefault()
                ?? throw new ArgumentException("Node must have at least one label", nameof(entity))
            : entity is global::Neo4j.Driver.IRelationship relationship
                ? relationship.Type
                : throw new ArgumentException("Entity must be a Node or Relationship", nameof(entity));
        var resolvedType = Labels.GetMostDerivedType(targetType, label)
            ?? throw new InvalidOperationException($"No type found for label '{label}' that is assignable to {targetType.Name}.");

        targetType = resolvedType;
    }

    var serializer = EntitySerializerRegistry.GetSerializer(targetType)
        ?? throw new InvalidOperationException($"No serializer registered for type {targetType.Name}.");

    var serialized = ConvertNodeToEntity(entity, relatedNodes);

    return serializer.Deserialize(serialized, targetType);
}

public Entity Serialize(IEntity entity)
{
    ArgumentNullException.ThrowIfNull(entity);

    var serializer = EntitySerializerRegistry.GetSerializer(entity.GetType()) ?? throw new GraphException(
        $"No serializer found for type {entity.GetType().Name}. Ensure it is registered in the EntitySerializerRegistry.");

    return serializer.Serialize(entity);
}

public T Deserialize<T>(Entity serializedEntity, Type? targetType = null, bool useMostDerivedType = false) where T : INode
{
    var actualType = targetType ?? typeof(T);

    if (useMostDerivedType)
    {
        actualType = Labels.GetMostDerivedType(actualType, serializedEntity.Label) ?? actualType;
    }

    var serializer = EntitySerializerRegistry.GetSerializer(actualType) ?? throw new GraphException($"No serializer found for type {actualType.Name}");

    var entity = serializer.Deserialize(serializedEntity, actualType);

    return (T)entity;
}

/// <summary>
/// Converts Neo4j query results to intermediate Entity representations that can be deserialized.
/// </summary>
/// <param name="record">The Neo4j record containing the main node and related nodes</param>
/// <param name="mainNodeKey">The key for the main node in the record (usually the node alias)</param>
/// <param name="relatedNodesKey">The key for the related nodes collection (usually "relatedNodes")</param>
/// <returns>An Entity representation that can be passed to a serializer</returns>
public Entity ConvertToIntermediateRepresentation(IRecord record, string mainNodeKey, string relatedNodesKey = "relatedNodes")
{
    // Get the main node from the record
    var mainNode = record[mainNodeKey].As<global::Neo4j.Driver.INode>();

    // Get the related nodes (complex properties) if they exist
    var relatedNodes = record.ContainsKey(relatedNodesKey) && (record[relatedNodesKey] is not null)
        ? record[relatedNodesKey].As<IList<IDictionary<string, object>>>()
        : new List<IDictionary<string, object>>();

    return ConvertNodeToEntity(mainNode, relatedNodes);
}

/// <summary>
/// Converts a single Neo4j node to an Entity representation.
/// </summary>
public Entity ConvertNodeToEntity(global::Neo4j.Driver.IEntity entity, IList<IDictionary<string, object>>? relatedNodes = null)
{
    // Find the schema for this node based on its labels
    var label = entity is global::Neo4j.Driver.INode node
        ? node.Labels.FirstOrDefault()
            ?? throw new ArgumentException("Node must have at least one label", nameof(entity))
        : entity is global::Neo4j.Driver.IRelationship relationship
            ? relationship.Type
            : throw new ArgumentException("Entity must be a Node or Relationship", nameof(entity));
    var schema = FindEntitySchema(label)
        ?? throw new InvalidOperationException($"No schema found for node with labels: {string.Join(", ", label)}");

    // Start with the main node properties
    var allProperties = new Dictionary<string, object?>(entity.Properties);

    // Process related nodes (complex properties) if we have them
    if (relatedNodes?.Count > 0)
    {
        ProcessComplexProperties(allProperties, relatedNodes, schema);
    }

    // Split properties into simple and complex based on schema
    var (simpleProperties, complexProperties) = SplitProperties(allProperties, schema);

    // Create the Entity representation
    return new Entity(
        Type: schema.Type,
        Label: schema.Label,
        SimpleProperties: simpleProperties,
        ComplexProperties: complexProperties
    );
}

private static (IReadOnlyDictionary<string, Property> Simple, IReadOnlyDictionary<string, Property> Complex)
    SplitProperties(Dictionary<string, object?> allProperties, EntitySchema schema)
{
    var simpleProps = new Dictionary<string, Property>();
    var complexProps = new Dictionary<string, Property>();

    foreach (var (key, value) in allProperties)
    {
        // Find the property schema to determine if it's simple or complex
        var propertySchema = schema.Properties.Values
            .FirstOrDefault(p => p.Neo4jPropertyName.Equals(key, StringComparison.OrdinalIgnoreCase));

        // Create Property with proper parameters based on schema
        var property = propertySchema != null
            ? new Property(
                PropertyInfo: propertySchema.PropertyInfo,
                Label: propertySchema.Neo4jPropertyName,
                IsNullable: propertySchema.IsNullable,
                Value: ConvertValueToSerialized(value))
            : new Property(
                PropertyInfo: null!, // We don't have PropertyInfo for unknown properties
                Label: key,
                IsNullable: true, // Default to nullable for unknown properties
                Value: ConvertValueToSerialized(value));

        if (propertySchema?.PropertyType is PropertyType.Simple or PropertyType.SimpleCollection)
        {
            simpleProps[key] = property;
        }
        else
        {
            // Complex properties, collections, or unknown properties default to complex
            complexProps[key] = property;
        }
    }

    return (simpleProps, complexProps);
}

private static Serialized? ConvertValueToSerialized(object? value)
{
    // First normalize any Neo4j-specific types to standard .NET types
    var normalizedValue = EntitySerializerBase.NormalizeValueForSerialization(value);

    return normalizedValue switch
    {
        null => null,
        Entity entity => entity,
        IList<Entity> entities => new EntityCollection(typeof(Entity), entities.ToList()),
        IEnumerable<object?> enumerable when enumerable.Where(item => item != null).All(item => GraphDataModel.IsSimple(item!.GetType())) =>
            new SimpleCollection(
                enumerable.Where(item => item != null)
                    .Select(item => new SimpleValue(item!, item!.GetType()))
                    .ToList(),
                enumerable.FirstOrDefault()?.GetType() ?? typeof(object)),
        IEnumerable<object?> enumerable =>
            new EntityCollection(
                typeof(Entity),
                enumerable.Select(ConvertValueToSerialized)
                    .Where(item => item != null)
                    .Cast<Entity>()
                    .ToList()),
        _ when GraphDataModel.IsSimple(normalizedValue.GetType()) => new SimpleValue(normalizedValue!, normalizedValue!.GetType()),
        _ => throw new NotSupportedException($"Cannot convert normalized value of type {normalizedValue?.GetType()} to Serialized")
    };
}
private static EntitySchema? FindEntitySchema(string label)
{
    var type = Labels.GetMostDerivedType(Labels.GetTypeFromLabel(label), label)
        ?? throw new InvalidOperationException($"No type found for label '{label}'");
    var serializer = EntitySerializerRegistry.GetSerializer(type);
    if (serializer != null)
    {
        return serializer.GetSchema();
    }

    return null;
}

private void ProcessComplexProperties(
    Dictionary<string, object?> properties,
    IList<IDictionary<string, object>> relatedNodes,
    EntitySchema schema)
{
    // Group related nodes by relationship type (which corresponds to property names)
    var groupedByProperty = relatedNodes
        .GroupBy(rn => rn["RelType"].As<string>())
        .ToDictionary(g => g.Key, g => g.ToList());

    foreach (var (relType, nodes) in groupedByProperty)
    {
        // Extract the property name from the relationship type
        // RelType format: "__PROP__{PropertyName}"
        var propertyName = ExtractPropertyNameFromRelType(relType);

        // Find the corresponding property schema
        var propertySchema = schema.Properties.Values
            .FirstOrDefault(p => p.Neo4jPropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

        if (propertySchema == null)
        {
            continue; // Skip unknown properties
        }

        // Process based on property type
        switch (propertySchema.PropertyType)
        {
            case PropertyType.Complex:
                ProcessComplexProperty(properties, propertyName, nodes.First(), propertySchema);
                break;

            case PropertyType.ComplexCollection:
                ProcessComplexCollectionProperty(properties, propertyName, nodes, propertySchema);
                break;

            case PropertyType.SimpleCollection:
                ProcessSimpleCollectionProperty(properties, propertyName, nodes);
                break;

            default:
                // Simple properties should already be in the main node properties
                break;
        }
    }
}

private void ProcessComplexProperty(
    Dictionary<string, object?> properties,
    string propertyName,
    IDictionary<string, object> relatedNode,
    PropertySchema propertySchema)
{
    // Get the actual node data
    var nodeData = relatedNode["Node"].As<global::Neo4j.Driver.INode>();

    // Create an Entity representation for this complex property
    var complexEntity = ConvertNodeToEntity(nodeData);

    // Store it as the property value
    properties[propertyName] = complexEntity;
}

private void ProcessComplexCollectionProperty(
    Dictionary<string, object?> properties,
    string propertyName,
    List<IDictionary<string, object>> relatedNodes,
    PropertySchema propertySchema)
{
    var collection = new List<Entity>();

    foreach (var relatedNode in relatedNodes)
    {
        var nodeData = relatedNode["Node"].As<global::Neo4j.Driver.INode>();
        var complexEntity = ConvertNodeToEntity(nodeData);
        collection.Add(complexEntity);
    }

    properties[propertyName] = collection;
}

private void ProcessSimpleCollectionProperty(
    Dictionary<string, object?> properties,
    string propertyName,
    List<IDictionary<string, object>> relatedNodes)
{
    // For simple collections, the values are stored in the relationship properties
    var collection = new List<object?>();

    foreach (var relatedNode in relatedNodes)
    {
        var relProps = relatedNode["RelationshipProperties"].As<IDictionary<string, object>>();

        // The value should be stored in a standard property (e.g., "value")
        if (relProps.TryGetValue("value", out var value))
        {
            collection.Add(value);
        }
    }

    properties[propertyName] = collection;
}

private string ExtractPropertyNameFromRelType(string relType)
{
    // RelType format: "__PROP__{PropertyName}"
    var prefix = GraphDataModel.PropertyRelationshipTypeNamePrefix;
    if (relType.StartsWith(prefix))
    {
        return relType[prefix.Length..];
    }

    return relType; // Fallback to the original if format doesn't match
}

private string ExtractNodeId(global::Neo4j.Driver.INode node)
{
    // Extract ID - could be from properties or element ID
    if (node.Properties.TryGetValue("id", out var idValue))
    {
        return idValue.ToString() ?? string.Empty;
    }

    // Fallback to Neo4j internal ID (though this isn't recommended for production)
    return node.ElementId;
}
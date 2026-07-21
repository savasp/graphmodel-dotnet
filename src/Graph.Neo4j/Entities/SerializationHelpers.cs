// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Entities;

using Cvoya.Graph.Neo4j.Serialization;
using Cvoya.Graph.Serialization;


internal static class SerializationHelpers
{
    public static Dictionary<string, object?> SerializeSimpleProperties(EntityInfo entity)
    {
        var properties = entity.SimpleProperties
            .Where(kv => kv.Value.Value is not null)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Value switch
                {
                    SimpleValue simple => SerializationBridge.ToNeo4jValue(simple.Object),
                    SimpleCollection collection => collection.Values.Select(v => SerializationBridge.ToNeo4jValue(v.Object)),
                    _ => throw new GraphException("Unexpected value type in simple properties")
                });

        properties[nameof(Graph.INode.Labels)] = entity.ActualLabels.Count > 0 ? entity.ActualLabels : Labels.GetCompatibleLabels(entity.ActualType);

        return properties;
    }

    public static bool IsLegacyStructuralProperty(EntityInfo entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        return entity.SimpleProperties.TryGetValue(propertyName, out var property) &&
            property.PropertyInfo.Name == propertyName &&
            property.PropertyInfo.DeclaringType is { } declaringType &&
            (declaringType == typeof(Graph.IEntity) ||
             declaringType == typeof(Graph.INode) ||
             declaringType == typeof(Graph.IRelationship) ||
             declaringType == typeof(Graph.Node) ||
             declaringType == typeof(Graph.Relationship) ||
             declaringType == typeof(Graph.DynamicNode) ||
             declaringType == typeof(Graph.DynamicRelationship));
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using Cvoya.Graph.Age.Serialization;
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
                    SimpleValue simple => SerializationBridge.ToAgeValue(simple.Object),
                    SimpleCollection collection => collection.Values.Select(v => SerializationBridge.ToAgeValue(v.Object)),
                    _ => throw new GraphException("Unexpected value type in simple properties")
                });

        properties[nameof(Graph.INode.Labels)] = entity.ActualLabels.Count > 0 ? entity.ActualLabels : Labels.GetCompatibleLabels(entity.ActualType);

        return properties;
    }
}
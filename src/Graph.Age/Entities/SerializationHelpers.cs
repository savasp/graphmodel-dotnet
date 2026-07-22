// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Entities;

using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;


internal static class SerializationHelpers
{
    public static Dictionary<string, object?> SerializeSimpleProperties(EntityInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var properties = SimpleCollectionStorageCodec.EncodeProperties(
            entity.SimpleProperties,
            omitNullPayloads: false,
            SerializationBridge.ToAgeValue);
        foreach (var (name, value) in ComplexCollectionStorageCodec.EncodeProperties(
            entity.ComplexProperties,
            SerializationBridge.ToAgeValue))
        {
            properties.Add(name, value);
        }

        return properties;
    }
}

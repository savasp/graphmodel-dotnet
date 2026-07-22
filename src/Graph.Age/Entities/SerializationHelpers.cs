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
        return SimpleCollectionStorageCodec.EncodeProperties(
            entity.SimpleProperties,
            omitNullPayloads: false,
            SerializationBridge.ToAgeValue);
    }
}

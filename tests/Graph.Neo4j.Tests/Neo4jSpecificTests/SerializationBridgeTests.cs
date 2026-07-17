// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Tests;

using Cvoya.Graph.Neo4j.Serialization;

public sealed class SerializationBridgeTests
{
    [Fact]
    public void ConcreteClrTypeIdentity_IsVersionIndependentAndResolvable()
    {
        var type = typeof(Dictionary<string, List<int>>);

        var identity = SerializationBridge.GetAssemblyQualifiedTypeName(type);

        Assert.DoesNotContain(", Version=", identity, StringComparison.Ordinal);
        Assert.Contains(", Culture=neutral, PublicKeyToken=", identity, StringComparison.Ordinal);
        Assert.Equal(type, Type.GetType(identity));
    }

    [Fact]
    public void MapMetadata_UsesVersionIndependentResolvableIdentity()
    {
        var type = typeof(Dictionary<string, List<int>>);

        var metadata = SerializationBridge.CreateMetadata(type);
        var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            metadata[SerializationBridge.MetadataPropertyName]);
        var identity = Assert.IsType<string>(values["type"]);

        Assert.DoesNotContain(", Version=", identity, StringComparison.Ordinal);
        Assert.Equal(type, Type.GetType(identity));
    }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

using Cvoya.Graph.Age.Serialization;

public sealed class SerializationBridgeTests
{
    [Fact]
    public void ConcreteClrTypeIdentity_IsVersionIndependentAndResolvable()
    {
        var type = typeof(Dictionary<string, List<int>>);

        var identity = SerializationBridge.CreateScalarMetadata(type);

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

    [Fact]
    public void NativeNameWithEncodedPrefix_IsNotClassifiedAsEncodedStorage()
    {
        const string label = "CvoyaN_NOT_AN_ENCODED_LABEL";

        var storageName = SerializationBridge.GetRootStorageName(label, relationship: false);

        Assert.Equal(label, storageName);
        Assert.False(SerializationBridge.IsEncodedRootStorageName(storageName, relationship: false));
    }
}

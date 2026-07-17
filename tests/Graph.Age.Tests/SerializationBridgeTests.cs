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
}

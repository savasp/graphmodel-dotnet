// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Tests;

public sealed class AgeDynamicEntityExtensionsTests
{
    [Fact]
    public void GetProperty_NullableValueElementList_PreservesNullElements()
    {
        var node = NodeWith(new List<object?> { null, 5L, null });

        var values = node.GetProperty<IList<int?>>("values");

        Assert.Equal(new int?[] { null, 5, null }, values);
    }

    [Fact]
    public void GetProperty_ReferenceElementList_PreservesNullElements()
    {
        var node = NodeWith(new List<object?> { null, "value" });

        var values = node.GetProperty<IList<string?>>("values");

        Assert.Equal(new string?[] { null, "value" }, values);
    }

    [Fact]
    public void GetProperty_NonNullableValueElementList_FailsClosedOnNullElements()
    {
        var node = NodeWith(new List<object?> { null, 5L });

        var exception = Assert.Throws<GraphException>(() => node.GetProperty<IList<int>>("values"));

        Assert.Contains("null element", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'values'", exception.Message, StringComparison.Ordinal);
    }

    private static DynamicNode NodeWith(object? value) => new(
        labels: ["ExtensionTest"],
        properties: new Dictionary<string, object?> { ["values"] = value });
}

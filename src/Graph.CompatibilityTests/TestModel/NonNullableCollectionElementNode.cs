// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A contract-test node whose simple collection rejects null elements on read.</summary>
[Node(Label = nameof(NonNullableCollectionElementNode))]
public record NonNullableCollectionElementNode : Node
{
    /// <summary>Gets or sets the key used to select this fixture deterministically.</summary>
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the collection whose element schema rejects null values.</summary>
    [Property(Label = "stored_values")]
    public List<string> Values { get; set; } = [];
}

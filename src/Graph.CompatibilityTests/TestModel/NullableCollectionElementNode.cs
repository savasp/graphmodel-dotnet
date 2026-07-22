// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A contract-test node whose simple collections permit null elements.</summary>
[Node(Label = nameof(NullableCollectionElementNode))]
public record NullableCollectionElementNode : Node
{
    /// <summary>Gets or sets the key used to select this fixture deterministically.</summary>
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the collection containing positional null elements.</summary>
    [Property(Label = "stored_values")]
    public List<string?> Values { get; set; } = [];

    /// <summary>Gets or sets the collection containing only null elements.</summary>
    [Property(Label = "all_null_values")]
    public List<string?> AllNullValues { get; set; } = [];
}

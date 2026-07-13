// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A contract-test node that owns one complex address value.</summary>
[Node("ContractAddressOwner")]
public sealed record ContractAddressOwner : Node
{
    /// <summary>Gets the owned address value.</summary>
    [ComplexProperty(RelationshipType = "CONTRACT_PRIMARY_ADDRESS")]
    public ContractAddressValue Address { get; init; } = new();
}

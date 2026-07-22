// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A contract-test node that owns a collection of complex address values.</summary>
[Node("ContractAddressCollectionOwner")]
public sealed record ContractAddressCollectionOwner : Node
{
    /// <summary>Gets the selector used by the provider contract tests.</summary>
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets the owned address values.</summary>
    public List<ContractAddressValue> Addresses { get; init; } = [];
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A domain node whose label collides with the contract complex-address value label.</summary>
[Node("ContractAddressValue")]
public sealed record ContractAddressNode : Node
{
    /// <summary>Gets the selector used by the provider contract tests.</summary>
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets the street.</summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>Gets the city.</summary>
    public string City { get; init; } = string.Empty;
}

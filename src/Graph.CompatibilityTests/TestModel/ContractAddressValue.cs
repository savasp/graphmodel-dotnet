// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>An address stored as a decomposed complex-property value.</summary>
public sealed record ContractAddressValue
{
    /// <summary>Gets the street.</summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>Gets the city.</summary>
    public string City { get; init; } = string.Empty;
}

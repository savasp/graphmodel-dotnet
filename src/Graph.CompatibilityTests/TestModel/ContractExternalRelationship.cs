// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A relationship used to certify provider-native external data interoperability.</summary>
[Relationship("CONTRACT_EXTERNAL_RELATIONSHIP")]
public sealed record ContractExternalRelationship : Relationship
{
    /// <summary>Gets or sets the fixture marker shared by the external graph elements.</summary>
    public string Marker { get; set; } = string.Empty;
}

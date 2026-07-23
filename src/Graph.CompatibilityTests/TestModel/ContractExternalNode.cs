// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A keyless node used to certify provider-native external data interoperability.</summary>
[Node("ContractExternalNode")]
public sealed record ContractExternalNode : Node
{
    /// <summary>Gets or sets the fixture marker shared by the external graph elements.</summary>
    public string Marker { get; set; } = string.Empty;

    /// <summary>Gets or sets this node's role in the external fixture.</summary>
    public string Role { get; set; } = string.Empty;
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>A domain node used to verify native binary scalar behavior across providers.</summary>
[Node("BinaryPropertyNode")]
public sealed record BinaryPropertyNode : Node
{
    /// <summary>Gets or sets the selector used by provider contract tests.</summary>
    public string TestKey { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the binary scalar value.</summary>
    public byte[] Data { get; set; } = [];
}

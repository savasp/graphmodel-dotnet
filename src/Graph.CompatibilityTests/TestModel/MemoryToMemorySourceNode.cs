// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Relationship("MemoryToMemorySourceNode")]
public record MemoryToMemorySourceNode : Relationship
{
    public MemoryToMemorySourceNode() : base(string.Empty, string.Empty) { }

    public MemoryToMemorySourceNode(string memoryId, string memorySourceNodeId) : base(memoryId, memorySourceNodeId) { }
}

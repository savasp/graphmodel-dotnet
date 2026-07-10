// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Relationship(Label = "ASSIGNED_TO")]
public record AssignedTo : Relationship
{
    public AssignedTo() : base(string.Empty, string.Empty) { }
    public AssignedTo(string startNodeId, string endNodeId) : base(startNodeId, endNodeId) { }

    [Property(IsRequired = true)]
    public DateTime AssignedDate { get; init; } = DateTime.UtcNow;

    [Property(IsRequired = true, MinLength = 1)]
    public string AssignedBy { get; init; } = string.Empty;
}

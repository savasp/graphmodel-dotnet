// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Memory : Node
{
    public string TestKey { get; init; } = Guid.NewGuid().ToString("N");

    [Property(IsRequired = true)]
    public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Property(IsRequired = true)]
    public required DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    [Property(IsRequired = true)]
    public required MemorySource CapturedBy { get; init; } = new MemorySource
    {
        Name = "BrainExpanded",
        Description = "BrainExpanded Graph Model",
        Version = "1.0.0",
        Device = "Web"
    };

    [Property(IsRequired = true)]
    public required Point Location { get; init; } = new Point { Longitude = 0, Latitude = 0, Height = 0 };

    [Property(IsRequired = true)]
    public required bool Deleted { get; init; } = false;

    [Property(IsRequired = true)]
    public string Text { get; init; } = string.Empty;
}

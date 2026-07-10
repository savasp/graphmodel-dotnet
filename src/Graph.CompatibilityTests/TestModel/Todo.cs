// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Todo : Memory
{
    [Property(Label = "note", IsRequired = true)]
    public required string Note { get; init; }

    [Property(Label = "done", IsRequired = true)]
    public required bool Done { get; init; } = false;

    [Property(Label = "due", IsRequired = true)]
    public required DateTime Due { get; init; } = DateTime.UtcNow;

    [Property(Label = "priority", IsRequired = true)]
    public required Priority Priority { get; init; } = Priority.Normal;

    // Additional fields for productivity service integration
    [Property(Label = "externalId")]
    public string? ExternalId { get; init; } // Original ID from the service (Google Tasks, M365 To Do)

    [Property(Label = "serviceType")]
    public string? ServiceType { get; init; } // "Google" or "Microsoft365" - null for local todos

    [Property(Label = "listId")]
    public string? ListId { get; init; } // ID of the list/project this todo belongs to

    [Property(Label = "listName")]
    public string? ListName { get; init; } // Name of the list/project

    [Property(Label = "completedAt")]
    public DateTime? CompletedAt { get; init; }

    [Property(Label = "categories")]
    public List<string> Categories { get; init; } = new();
}

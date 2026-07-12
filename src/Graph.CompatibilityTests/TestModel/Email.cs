// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record Email : Memory
{
    [Property(IsRequired = true)]
    public required string Subject { get; init; }

    [Property(IsRequired = true)]
    public required string From { get; init; }

    public required List<string> To { get; init; } = new();
    public required List<string> Cc { get; init; } = new();
    public required List<string> Bcc { get; init; } = new();
    public string? MessageId { get; init; }
    public string? ThreadId { get; init; }

    [Property(IsRequired = true)]
    public string Body { get; init; } = string.Empty;

    public string? BodyHtml { get; init; }
    public bool IsRead { get; init; }
    public bool IsImportant { get; init; }
    public DateTime SentAt { get; init; }
    public DateTime? ReceivedAt { get; init; }
    public string? ExternalId { get; init; } // Original ID from the service
    public string? ServiceType { get; init; } // "Google" or "Microsoft365"
}

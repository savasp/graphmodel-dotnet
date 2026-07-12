// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

public record EmailAttachment : Node
{
    public string Name { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public long Size { get; init; }
    public string? AttachmentId { get; init; }
    public bool IsInline { get; init; }
}
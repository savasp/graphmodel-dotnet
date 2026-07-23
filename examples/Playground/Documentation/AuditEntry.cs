// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph;

namespace Documentation;

// snippet-start: root-model-audit-entry
[Node(Label = "AuditEntry")]
public record AuditEntry : Node
{
    public string Message { get; set; } = string.Empty;
}
// snippet-end: root-model-audit-entry

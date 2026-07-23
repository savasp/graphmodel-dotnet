// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Discriminating fixture for the common provider query-expression contract. Its values are
/// intentionally simple properties so tests isolate expression semantics from traversal and
/// complex-property storage behavior.
/// </summary>
public sealed record QueryExpressionCoverageNode : Node
{
    /// <summary>Gets or sets the per-test scope that isolates seeded rows.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Gets or sets the stable row marker used in result assertions.</summary>
    public string Marker { get; set; } = string.Empty;

    /// <summary>Gets or sets the category used for membership, ordering, and grouping predicates.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the string expression input.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the nullable string expression input.</summary>
    public string? OptionalText { get; set; }

    /// <summary>Gets or sets the integer expression input.</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets the decimal aggregation input.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the temporal expression input.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Gets or sets the boolean expression input.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the nullable-element collection used for membership assertions.</summary>
    public List<string?> Tags { get; set; } = [];
}

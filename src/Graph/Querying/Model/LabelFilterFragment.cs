// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>Describes a value-based any/all label filter against a bound node scope.</summary>
public sealed record LabelFilterFragment
{
    /// <summary>Initializes a label filter.</summary>
    public LabelFilterFragment(string alias, IReadOnlyList<string> labels, GraphLabelMatch match)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        Alias = alias;
        Labels = QueryModelGuard.CopyRequiredList(labels, nameof(labels));
        if (Labels.Count == 0 || Labels.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A label filter requires non-empty labels.", nameof(labels));
        QueryModelGuard.RequireDefinedEnum(match, nameof(match));
        Match = match;
    }

    /// <summary>Gets the node scope alias.</summary>
    public string Alias { get; }

    /// <summary>Gets the requested labels.</summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>Gets whether any or all labels must match.</summary>
    public GraphLabelMatch Match { get; }

    /// <summary>Gets whether the operator appeared after a projection in the LINQ chain.</summary>
    public bool AppliedAfterProjection { get; init; }

    /// <summary>Gets whether the operator appeared after paging in the LINQ chain.</summary>
    public bool AppliedAfterPaging { get; init; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a node pattern in a Cypher path.
/// </summary>
public sealed record NodePattern : PatternElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodePattern"/> class.
    /// </summary>
    /// <param name="alias">The optional node alias.</param>
    /// <param name="labels">The node labels.</param>
    public NodePattern(string? alias, IReadOnlyList<string> labels)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Labels = ArgumentValidation.StringList(labels, nameof(labels));
    }

    /// <summary>
    /// Gets the optional node alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the node labels.
    /// </summary>
    public IReadOnlyList<string> Labels { get; }
}

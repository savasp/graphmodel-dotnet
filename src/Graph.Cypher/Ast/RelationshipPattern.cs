// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a relationship pattern in a Cypher path.
/// </summary>
public sealed record RelationshipPattern : PatternElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipPattern"/> class.
    /// </summary>
    /// <param name="alias">The optional relationship alias.</param>
    /// <param name="type">The optional relationship type.</param>
    /// <param name="direction">The relationship direction.</param>
    /// <param name="depth">The optional variable-length depth range.</param>
    public RelationshipPattern(string? alias, string? type, CypherDirection direction, DepthRange? depth)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Type = ArgumentValidation.OptionalName(type, nameof(type));
        Direction = ArgumentValidation.DefinedEnum(direction, nameof(direction));
        Depth = depth;
    }

    /// <summary>
    /// Gets the optional relationship alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the optional relationship type.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the relationship direction.
    /// </summary>
    public CypherDirection Direction { get; }

    /// <summary>
    /// Gets the optional variable-length depth range.
    /// </summary>
    public DepthRange? Depth { get; }
}

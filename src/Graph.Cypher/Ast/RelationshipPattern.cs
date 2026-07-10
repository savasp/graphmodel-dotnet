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
    /// Initializes a new instance of the <see cref="RelationshipPattern"/> class matching a single
    /// relationship type, or any type.
    /// </summary>
    /// <param name="alias">The optional relationship alias.</param>
    /// <param name="type">The relationship type name, or <see langword="null"/> for any type. The
    /// name is one identifier — a <c>|</c> in it is part of the name, not an alternation.</param>
    /// <param name="direction">The relationship direction.</param>
    /// <param name="depth">The optional variable-length depth range.</param>
    public RelationshipPattern(string? alias, string? type, CypherDirection direction, DepthRange? depth)
        : this(
            alias,
            ArgumentValidation.OptionalName(type, nameof(type)) is { } single ? [single] : [],
            direction,
            depth)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipPattern"/> class matching any of
    /// several relationship types.
    /// </summary>
    /// <param name="alias">The optional relationship alias.</param>
    /// <param name="types">The relationship type names to match as alternatives; empty matches any
    /// type. Each entry is one identifier — renderers join and escape them, so a <c>|</c> inside a
    /// name is part of that name, not an alternation separator.</param>
    /// <param name="direction">The relationship direction.</param>
    /// <param name="depth">The optional variable-length depth range.</param>
    public RelationshipPattern(string? alias, IReadOnlyList<string> types, CypherDirection direction, DepthRange? depth)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Types = ArgumentValidation.StringList(types, nameof(types));
        Direction = ArgumentValidation.DefinedEnum(direction, nameof(direction));
        Depth = depth;
    }

    /// <summary>
    /// Gets the optional relationship alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the relationship type names matched as alternatives; empty matches any type.
    /// </summary>
    public IReadOnlyList<string> Types { get; }

    /// <summary>
    /// Gets the relationship direction.
    /// </summary>
    public CypherDirection Direction { get; }

    /// <summary>
    /// Gets the optional variable-length depth range.
    /// </summary>
    public DepthRange? Depth { get; }
}

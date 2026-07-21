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
    /// <param name="isComplexProperty">Whether this pattern traverses provider-owned complex-property storage.</param>
    public RelationshipPattern(
        string? alias,
        string? type,
        CypherDirection direction,
        DepthRange? depth,
        bool isComplexProperty = false)
        : this(
            alias,
            direction,
            depth,
            ArgumentValidation.OptionalName(type, nameof(type)) is { } single ? [single] : [],
            nameof(type),
            isComplexProperty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipPattern"/> class matching any of
    /// several relationship types.
    /// </summary>
    /// <param name="alias">The optional relationship alias.</param>
    /// <param name="direction">The relationship direction.</param>
    /// <param name="depth">The optional variable-length depth range.</param>
    /// <param name="types">The relationship type names to match as alternatives; empty matches any
    /// type. Each entry is one identifier — renderers join and escape them, so a <c>|</c> inside a
    /// name is part of that name, not an alternation separator.</param>
    /// <param name="isComplexProperty">Whether this pattern traverses provider-owned complex-property storage.</param>
    public RelationshipPattern(
        string? alias,
        CypherDirection direction,
        DepthRange? depth,
        IReadOnlyList<string> types,
        bool isComplexProperty = false)
        : this(alias, direction, depth, types, nameof(types), isComplexProperty)
    {
    }

    private RelationshipPattern(
        string? alias,
        CypherDirection direction,
        DepthRange? depth,
        IReadOnlyList<string> types,
        string typesParameterName,
        bool isComplexProperty)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Types = ArgumentValidation.StringList(types, typesParameterName);
        Direction = ArgumentValidation.DefinedEnum(direction, nameof(direction));
        Depth = depth;
        IsComplexProperty = isComplexProperty;
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

    /// <summary>
    /// Gets a value indicating whether this pattern traverses provider-owned complex-property
    /// storage rather than an ordinary domain relationship.
    /// </summary>
    public bool IsComplexProperty { get; }
}

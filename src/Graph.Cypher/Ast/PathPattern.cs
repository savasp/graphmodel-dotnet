// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher path pattern.
/// </summary>
public sealed record PathPattern
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathPattern"/> class.
    /// </summary>
    /// <param name="elements">The alternating node and relationship elements in the path.</param>
    public PathPattern(IReadOnlyList<PatternElement> elements)
        : this(elements, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathPattern"/> class.
    /// </summary>
    /// <param name="elements">The alternating node and relationship elements in the path.</param>
    /// <param name="alias">The optional alias bound to the complete path.</param>
    public PathPattern(IReadOnlyList<PatternElement> elements, string? alias)
    {
        Elements = ArgumentValidation.RequiredList(elements, nameof(elements));
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        ValidateShape(Elements, nameof(elements));
    }

    /// <summary>
    /// Gets the alternating node and relationship elements in the path.
    /// </summary>
    public IReadOnlyList<PatternElement> Elements { get; }

    /// <summary>Gets the optional alias bound to the complete path.</summary>
    public string? Alias { get; }

    private static void ValidateShape(IReadOnlyList<PatternElement> elements, string parameterName)
    {
        if (elements[0] is not NodePattern || elements[^1] is not NodePattern)
        {
            throw new ArgumentException("A path pattern must start and end with a node pattern.", parameterName);
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var hasExpectedType = i % 2 == 0
                ? elements[i] is NodePattern
                : elements[i] is RelationshipPattern;

            if (!hasExpectedType)
            {
                throw new ArgumentException("A path pattern must alternate node and relationship patterns.", parameterName);
            }
        }
    }
}

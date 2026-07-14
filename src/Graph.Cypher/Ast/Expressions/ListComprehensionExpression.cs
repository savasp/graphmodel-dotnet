// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a Cypher list comprehension with an optional predicate, projection, or both.
/// </summary>
public sealed record ListComprehensionExpression : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListComprehensionExpression"/> class.
    /// </summary>
    /// <param name="source">The expression that produces the source list.</param>
    /// <param name="iteratorAlias">The alias bound to each source item.</param>
    /// <param name="predicate">The optional expression used to filter source items.</param>
    /// <param name="projection">The optional expression used to project source items.</param>
    public ListComprehensionExpression(
        CypherExpression source,
        string iteratorAlias,
        CypherExpression? predicate = null,
        CypherExpression? projection = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        IteratorAlias = ArgumentValidation.RequiredName(iteratorAlias, nameof(iteratorAlias));

        if (predicate is null && projection is null)
        {
            throw new ArgumentException(
                "A list comprehension must define a predicate, a projection, or both.",
                nameof(projection));
        }

        Predicate = predicate;
        Projection = projection;
    }

    /// <summary>Gets the expression that produces the source list.</summary>
    public CypherExpression Source { get; }

    /// <summary>Gets the alias bound to each source item.</summary>
    public string IteratorAlias { get; }

    /// <summary>Gets the optional expression used to filter source items.</summary>
    public CypherExpression? Predicate { get; }

    /// <summary>Gets the optional expression used to project source items.</summary>
    public CypherExpression? Projection { get; }
}

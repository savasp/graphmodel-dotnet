// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a Cypher <c>ALL</c> quantifier expression.
/// </summary>
public sealed record AllExpression : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AllExpression"/> class.
    /// </summary>
    /// <param name="iteratorAlias">The alias bound to each source item.</param>
    /// <param name="source">The expression that produces the source list.</param>
    /// <param name="predicate">The expression that every source item must satisfy.</param>
    public AllExpression(
        string iteratorAlias,
        CypherExpression source,
        CypherExpression predicate)
    {
        IteratorAlias = ArgumentValidation.RequiredName(iteratorAlias, nameof(iteratorAlias));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>Gets the alias bound to each source item.</summary>
    public string IteratorAlias { get; }

    /// <summary>Gets the expression that produces the source list.</summary>
    public CypherExpression Source { get; }

    /// <summary>Gets the expression that every source item must satisfy.</summary>
    public CypherExpression Predicate { get; }
}

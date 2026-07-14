// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a Cypher <c>reduce</c> expression.
/// </summary>
public sealed record ReduceExpression : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReduceExpression"/> class.
    /// </summary>
    /// <param name="accumulatorAlias">The alias bound to the accumulated value.</param>
    /// <param name="seed">The expression that produces the initial accumulated value.</param>
    /// <param name="iteratorAlias">The alias bound to each source item.</param>
    /// <param name="source">The expression that produces the source list.</param>
    /// <param name="reducer">The expression that produces the next accumulated value.</param>
    public ReduceExpression(
        string accumulatorAlias,
        CypherExpression seed,
        string iteratorAlias,
        CypherExpression source,
        CypherExpression reducer)
    {
        AccumulatorAlias = ArgumentValidation.RequiredName(accumulatorAlias, nameof(accumulatorAlias));
        IteratorAlias = ArgumentValidation.RequiredName(iteratorAlias, nameof(iteratorAlias));
        if (string.Equals(AccumulatorAlias, IteratorAlias, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The accumulator and iterator aliases must be different.",
                nameof(iteratorAlias));
        }

        Seed = seed ?? throw new ArgumentNullException(nameof(seed));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
    }

    /// <summary>Gets the alias bound to the accumulated value.</summary>
    public string AccumulatorAlias { get; }

    /// <summary>Gets the expression that produces the initial accumulated value.</summary>
    public CypherExpression Seed { get; }

    /// <summary>Gets the alias bound to each source item.</summary>
    public string IteratorAlias { get; }

    /// <summary>Gets the expression that produces the source list.</summary>
    public CypherExpression Source { get; }

    /// <summary>Gets the expression that produces the next accumulated value.</summary>
    public CypherExpression Reducer { get; }
}

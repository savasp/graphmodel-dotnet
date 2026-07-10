// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents an expression in an ORDER BY clause.
/// </summary>
public sealed record OrderByItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByItem"/> class.
    /// </summary>
    /// <param name="expression">The expression to sort by.</param>
    /// <param name="descending">Whether sorting is descending.</param>
    public OrderByItem(CypherExpression expression, bool descending)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression = expression;
        Descending = descending;
    }

    /// <summary>
    /// Gets the expression to sort by.
    /// </summary>
    public CypherExpression Expression { get; }

    /// <summary>
    /// Gets a value indicating whether sorting is descending.
    /// </summary>
    public bool Descending { get; }
}

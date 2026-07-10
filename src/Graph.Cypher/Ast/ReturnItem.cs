// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents an expression projected by RETURN or WITH.
/// </summary>
public sealed record ReturnItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnItem"/> class.
    /// </summary>
    /// <param name="expression">The projected expression.</param>
    /// <param name="alias">The optional projection alias.</param>
    public ReturnItem(CypherExpression expression, string? alias)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression = expression;
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
    }

    /// <summary>
    /// Gets the projected expression.
    /// </summary>
    public CypherExpression Expression { get; }

    /// <summary>
    /// Gets the optional projection alias.
    /// </summary>
    public string? Alias { get; }
}

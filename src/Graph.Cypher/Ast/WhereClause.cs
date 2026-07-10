// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher WHERE clause.
/// </summary>
public sealed record WhereClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WhereClause"/> class.
    /// </summary>
    /// <param name="predicate">The predicate expression.</param>
    public WhereClause(CypherExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        Predicate = predicate;
    }

    /// <summary>
    /// Gets the predicate expression.
    /// </summary>
    public CypherExpression Predicate { get; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher LIMIT clause.
/// </summary>
public sealed record LimitClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LimitClause"/> class.
    /// </summary>
    /// <param name="count">The maximum number of rows to return.</param>
    public LimitClause(CypherExpression count)
    {
        ArgumentNullException.ThrowIfNull(count);

        Count = count;
    }

    /// <summary>
    /// Gets the maximum number of rows to return.
    /// </summary>
    public CypherExpression Count { get; }
}

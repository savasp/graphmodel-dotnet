// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher SKIP clause.
/// </summary>
public sealed record SkipClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipClause"/> class.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    public SkipClause(CypherExpression count)
    {
        ArgumentNullException.ThrowIfNull(count);

        Count = count;
    }

    /// <summary>
    /// Gets the number of rows to skip.
    /// </summary>
    public CypherExpression Count { get; }
}

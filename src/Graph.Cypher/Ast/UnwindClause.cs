// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher UNWIND clause.
/// </summary>
public sealed record UnwindClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnwindClause"/> class.
    /// </summary>
    /// <param name="source">The expression to unwind.</param>
    /// <param name="alias">The alias bound for each unwound value.</param>
    public UnwindClause(CypherExpression source, string alias)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source;
        Alias = ArgumentValidation.RequiredName(alias, nameof(alias));
    }

    /// <summary>
    /// Gets the expression to unwind.
    /// </summary>
    public CypherExpression Source { get; }

    /// <summary>
    /// Gets the alias bound for each unwound value.
    /// </summary>
    public string Alias { get; }
}

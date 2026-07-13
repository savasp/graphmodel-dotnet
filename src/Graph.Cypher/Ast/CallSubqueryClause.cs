// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a scoped Cypher subquery: <c>CALL { WITH &lt;imports&gt; &lt;body&gt; }</c>. Distinct from
/// <see cref="CallClause"/> (a <c>CALL proc() YIELD</c> procedure invocation), this executes an inner
/// clause pipeline once per outer row, importing the named variables, and appends the columns its
/// terminal <c>RETURN</c> produces to the outer row. It is the form correlated collection
/// projections use when a pattern comprehension cannot express the shape (ordering, aggregation, or
/// nested grouping over the correlated collection).
/// </summary>
public sealed record CallSubqueryClause : ICypherClause
{
    /// <summary>
    /// Initializes a scoped subquery.
    /// </summary>
    /// <param name="importedVariables">The outer variables imported into the subquery scope.</param>
    /// <param name="body">The inner clause pipeline, ending in a <c>RETURN</c>.</param>
    public CallSubqueryClause(IReadOnlyList<string> importedVariables, IReadOnlyList<ICypherClause> body)
    {
        ImportedVariables = ArgumentValidation.List(importedVariables, nameof(importedVariables));
        Body = ArgumentValidation.RequiredList(body, nameof(body));
    }

    /// <summary>Gets the outer variables imported into the subquery scope.</summary>
    public IReadOnlyList<string> ImportedVariables { get; }

    /// <summary>Gets the inner clause pipeline.</summary>
    public IReadOnlyList<ICypherClause> Body { get; }
}

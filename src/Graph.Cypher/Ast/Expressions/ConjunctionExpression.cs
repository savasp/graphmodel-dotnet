// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents top-level predicate fragments joined by AND without adding expression-level grouping.
/// </summary>
public sealed record ConjunctionExpression : CypherExpression
{
    /// <summary>Initializes a conjunction.</summary>
    public ConjunctionExpression(IReadOnlyList<CypherExpression> predicates)
    {
        Predicates = ArgumentValidation.RequiredList(predicates, nameof(predicates));
    }

    /// <summary>Gets the predicates.</summary>
    public IReadOnlyList<CypherExpression> Predicates { get; }
}

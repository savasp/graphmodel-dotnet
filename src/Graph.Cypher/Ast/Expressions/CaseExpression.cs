// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents a searched CASE expression.</summary>
public sealed record CaseExpression : CypherExpression
{
    /// <summary>Initializes a searched CASE expression.</summary>
    public CaseExpression(CypherExpression condition, CypherExpression whenTrue, CypherExpression? whenFalse = null)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        WhenTrue = whenTrue ?? throw new ArgumentNullException(nameof(whenTrue));
        WhenFalse = whenFalse;
    }

    /// <summary>Gets the condition.</summary>
    public CypherExpression Condition { get; }

    /// <summary>Gets the expression used when the condition is true.</summary>
    public CypherExpression WhenTrue { get; }

    /// <summary>Gets the expression used when the condition is false.</summary>
    public CypherExpression? WhenFalse { get; }
}

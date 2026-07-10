// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents indexed collection access.</summary>
public sealed record IndexExpression : CypherExpression
{
    /// <summary>Initializes indexed access.</summary>
    public IndexExpression(CypherExpression target, CypherExpression index)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Index = index ?? throw new ArgumentNullException(nameof(index));
    }

    /// <summary>Gets the indexed expression.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the index expression.</summary>
    public CypherExpression Index { get; }
}

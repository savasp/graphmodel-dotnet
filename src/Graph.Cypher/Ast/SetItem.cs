// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

using Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents one property assignment in a Cypher SET clause.</summary>
public sealed record SetItem
{
    /// <summary>Initializes a Cypher property assignment.</summary>
    /// <param name="target">The property access to assign.</param>
    /// <param name="value">The assigned value expression.</param>
    public SetItem(CypherExpression target, CypherExpression value)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the assigned property access.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the assigned value expression.</summary>
    public CypherExpression Value { get; }
}

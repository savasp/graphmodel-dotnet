// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a unary Cypher expression.
/// </summary>
public sealed record UnaryExpression : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnaryExpression"/> class.
    /// </summary>
    /// <param name="op">The unary operator.</param>
    /// <param name="operand">The operand.</param>
    public UnaryExpression(CypherUnaryOperator op, CypherExpression operand)
    {
        ArgumentNullException.ThrowIfNull(operand);

        Op = ArgumentValidation.DefinedEnum(op, nameof(op));
        Operand = operand;
    }

    /// <summary>
    /// Gets the unary operator.
    /// </summary>
    public CypherUnaryOperator Op { get; }

    /// <summary>
    /// Gets the operand.
    /// </summary>
    public CypherExpression Operand { get; }
}

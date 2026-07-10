// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a binary Cypher expression.
/// </summary>
public sealed record BinaryExpression : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryExpression"/> class.
    /// </summary>
    /// <param name="op">The binary operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    public BinaryExpression(CypherBinaryOperator op, CypherExpression left, CypherExpression right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        Op = ArgumentValidation.DefinedEnum(op, nameof(op));
        Left = left;
        Right = right;
    }

    /// <summary>
    /// Gets the binary operator.
    /// </summary>
    public CypherBinaryOperator Op { get; }

    /// <summary>
    /// Gets the left operand.
    /// </summary>
    public CypherExpression Left { get; }

    /// <summary>
    /// Gets the right operand.
    /// </summary>
    public CypherExpression Right { get; }
}

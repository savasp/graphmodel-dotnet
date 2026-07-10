// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

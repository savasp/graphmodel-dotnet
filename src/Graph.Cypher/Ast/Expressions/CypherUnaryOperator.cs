// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Defines supported Cypher unary operators.
/// </summary>
public enum CypherUnaryOperator
{
    /// <summary>
    /// Logical negation.
    /// </summary>
    Not,

    /// <summary>
    /// Null test.
    /// </summary>
    IsNull,

    /// <summary>
    /// Non-null test.
    /// </summary>
    IsNotNull,

    /// <summary>
    /// Numeric negation.
    /// </summary>
    Negate
}

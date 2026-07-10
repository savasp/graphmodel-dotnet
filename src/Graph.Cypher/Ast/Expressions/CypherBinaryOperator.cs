// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Defines supported Cypher binary operators.
/// </summary>
public enum CypherBinaryOperator
{
    /// <summary>
    /// Logical conjunction.
    /// </summary>
    And,

    /// <summary>
    /// Logical disjunction.
    /// </summary>
    Or,

    /// <summary>
    /// Equality comparison.
    /// </summary>
    Equal,

    /// <summary>
    /// Inequality comparison.
    /// </summary>
    NotEqual,

    /// <summary>
    /// Less-than comparison.
    /// </summary>
    LessThan,

    /// <summary>
    /// Less-than-or-equal comparison.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Greater-than comparison.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater-than-or-equal comparison.
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Addition.
    /// </summary>
    Add,

    /// <summary>
    /// Subtraction.
    /// </summary>
    Subtract,

    /// <summary>
    /// Multiplication.
    /// </summary>
    Multiply,

    /// <summary>
    /// Division.
    /// </summary>
    Divide,

    /// <summary>
    /// Modulo.
    /// </summary>
    Modulo,

    /// <summary>
    /// Prefix string comparison.
    /// </summary>
    StartsWith,

    /// <summary>
    /// Suffix string comparison.
    /// </summary>
    EndsWith,

    /// <summary>
    /// Containment comparison.
    /// </summary>
    Contains,

    /// <summary>
    /// Collection membership comparison.
    /// </summary>
    In,

    /// <summary>
    /// Regular expression comparison.
    /// </summary>
    Matches
}

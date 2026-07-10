// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

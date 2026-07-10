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

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents an expression in an ORDER BY clause.
/// </summary>
public sealed record OrderByItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByItem"/> class.
    /// </summary>
    /// <param name="expression">The expression to sort by.</param>
    /// <param name="descending">Whether sorting is descending.</param>
    public OrderByItem(CypherExpression expression, bool descending)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression = expression;
        Descending = descending;
    }

    /// <summary>
    /// Gets the expression to sort by.
    /// </summary>
    public CypherExpression Expression { get; }

    /// <summary>
    /// Gets a value indicating whether sorting is descending.
    /// </summary>
    public bool Descending { get; }
}

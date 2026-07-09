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

using Cvoya.Graph.Model.Cypher.Ast.Expressions;
using Cvoya.Graph.Model.Cypher.Internal;

namespace Cvoya.Graph.Model.Cypher.Ast;

/// <summary>
/// Represents an expression projected by RETURN or WITH.
/// </summary>
public sealed record ReturnItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnItem"/> class.
    /// </summary>
    /// <param name="expression">The projected expression.</param>
    /// <param name="alias">The optional projection alias.</param>
    public ReturnItem(CypherExpression expression, string? alias)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression = expression;
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
    }

    /// <summary>
    /// Gets the projected expression.
    /// </summary>
    public CypherExpression Expression { get; }

    /// <summary>
    /// Gets the optional projection alias.
    /// </summary>
    public string? Alias { get; }
}

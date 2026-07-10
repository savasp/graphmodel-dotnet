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
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher UNWIND clause.
/// </summary>
public sealed record UnwindClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnwindClause"/> class.
    /// </summary>
    /// <param name="source">The expression to unwind.</param>
    /// <param name="alias">The alias bound for each unwound value.</param>
    public UnwindClause(CypherExpression source, string alias)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source;
        Alias = ArgumentValidation.RequiredName(alias, nameof(alias));
    }

    /// <summary>
    /// Gets the expression to unwind.
    /// </summary>
    public CypherExpression Source { get; }

    /// <summary>
    /// Gets the alias bound for each unwound value.
    /// </summary>
    public string Alias { get; }
}

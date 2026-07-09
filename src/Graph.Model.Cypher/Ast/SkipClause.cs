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

namespace Cvoya.Graph.Model.Cypher.Ast;

/// <summary>
/// Represents a Cypher SKIP clause.
/// </summary>
public sealed record SkipClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipClause"/> class.
    /// </summary>
    /// <param name="count">The number of rows to skip.</param>
    public SkipClause(CypherExpression count)
    {
        ArgumentNullException.ThrowIfNull(count);

        Count = count;
    }

    /// <summary>
    /// Gets the number of rows to skip.
    /// </summary>
    public CypherExpression Count { get; }
}

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

using Cvoya.Graph.Model.Cypher.Internal;

namespace Cvoya.Graph.Model.Cypher.Ast;

/// <summary>
/// Represents a Cypher MATCH or OPTIONAL MATCH clause.
/// </summary>
public sealed record MatchClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatchClause"/> class.
    /// </summary>
    /// <param name="patterns">The path patterns matched by the clause.</param>
    /// <param name="optional">Whether this clause is an OPTIONAL MATCH.</param>
    public MatchClause(IReadOnlyList<PathPattern> patterns, bool optional)
    {
        Patterns = ArgumentValidation.RequiredList(patterns, nameof(patterns));
        Optional = optional;
    }

    /// <summary>
    /// Gets the path patterns matched by the clause.
    /// </summary>
    public IReadOnlyList<PathPattern> Patterns { get; }

    /// <summary>
    /// Gets a value indicating whether this clause is an OPTIONAL MATCH.
    /// </summary>
    public bool Optional { get; }
}

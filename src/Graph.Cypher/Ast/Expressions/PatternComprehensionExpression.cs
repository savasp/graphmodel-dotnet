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

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents a Cypher pattern comprehension.</summary>
public sealed record PatternComprehensionExpression : CypherExpression
{
    /// <summary>Initializes a pattern comprehension.</summary>
    public PatternComprehensionExpression(
        PathPattern pattern,
        CypherExpression projection,
        CypherExpression? predicate = null)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        Predicate = predicate;
    }

    /// <summary>Gets the pattern.</summary>
    public PathPattern Pattern { get; }

    /// <summary>Gets the projected expression.</summary>
    public CypherExpression Projection { get; }

    /// <summary>Gets the optional predicate.</summary>
    public CypherExpression? Predicate { get; }
}

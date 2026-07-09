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

namespace Cvoya.Graph.Model.Cypher.Ast.Expressions;

/// <summary>Represents a searched CASE expression.</summary>
public sealed record CaseExpression : CypherExpression
{
    /// <summary>Initializes a searched CASE expression.</summary>
    public CaseExpression(CypherExpression condition, CypherExpression whenTrue, CypherExpression? whenFalse = null)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        WhenTrue = whenTrue ?? throw new ArgumentNullException(nameof(whenTrue));
        WhenFalse = whenFalse;
    }

    /// <summary>Gets the condition.</summary>
    public CypherExpression Condition { get; }

    /// <summary>Gets the expression used when the condition is true.</summary>
    public CypherExpression WhenTrue { get; }

    /// <summary>Gets the expression used when the condition is false.</summary>
    public CypherExpression? WhenFalse { get; }
}

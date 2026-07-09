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

namespace Cvoya.Graph.Model.Cypher.Ast.Expressions;

/// <summary>Represents an EXISTS or COUNT pattern subquery expression.</summary>
public sealed record PatternSubqueryExpression : CypherExpression
{
    /// <summary>Initializes a pattern subquery.</summary>
    public PatternSubqueryExpression(
        PatternSubqueryKind kind,
        PathPattern pattern,
        CypherExpression? predicate = null)
    {
        Kind = ArgumentValidation.DefinedEnum(kind, nameof(kind));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Predicate = predicate;
    }

    /// <summary>Gets the subquery kind.</summary>
    public PatternSubqueryKind Kind { get; }

    /// <summary>Gets the matched pattern.</summary>
    public PathPattern Pattern { get; }

    /// <summary>Gets the optional pattern predicate.</summary>
    public CypherExpression? Predicate { get; }
}

/// <summary>Defines pattern subquery result kinds.</summary>
public enum PatternSubqueryKind
{
    /// <summary>An existence predicate.</summary>
    Exists,

    /// <summary>A row count expression.</summary>
    Count,
}

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

/// <summary>
/// Represents a label predicate against a target expression.
/// </summary>
public sealed record LabelTest : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LabelTest"/> class.
    /// </summary>
    /// <param name="target">The expression to test.</param>
    /// <param name="labels">The labels that must match.</param>
    public LabelTest(CypherExpression target, IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
        Labels = ArgumentValidation.RequiredStringList(labels, nameof(labels));
    }

    /// <summary>
    /// Gets the expression to test.
    /// </summary>
    public CypherExpression Target { get; }

    /// <summary>
    /// Gets the labels that must match.
    /// </summary>
    public IReadOnlyList<string> Labels { get; }
}

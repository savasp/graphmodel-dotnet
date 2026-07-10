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

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents property access on a Cypher expression.
/// </summary>
public sealed record PropertyAccess : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyAccess"/> class.
    /// </summary>
    /// <param name="target">The expression whose property is accessed.</param>
    /// <param name="property">The property name.</param>
    public PropertyAccess(CypherExpression target, string property)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
        Property = ArgumentValidation.RequiredName(property, nameof(property));
    }

    /// <summary>
    /// Gets the expression whose property is accessed.
    /// </summary>
    public CypherExpression Target { get; }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Property { get; }
}

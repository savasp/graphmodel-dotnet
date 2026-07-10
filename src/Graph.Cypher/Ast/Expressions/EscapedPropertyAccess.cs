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

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents dynamic property access whose identifier must be escaped by the renderer.</summary>
public sealed record EscapedPropertyAccess : CypherExpression
{
    /// <summary>Initializes dynamic property access.</summary>
    public EscapedPropertyAccess(CypherExpression target, string property)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
    }

    /// <summary>Gets the target expression.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the unescaped property identifier.</summary>
    public string Property { get; }
}

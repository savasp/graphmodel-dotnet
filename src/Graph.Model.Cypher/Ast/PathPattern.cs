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
/// Represents a Cypher path pattern.
/// </summary>
public sealed record PathPattern
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathPattern"/> class.
    /// </summary>
    /// <param name="elements">The alternating node and relationship elements in the path.</param>
    public PathPattern(IReadOnlyList<PatternElement> elements)
    {
        Elements = ArgumentValidation.RequiredList(elements, nameof(elements));
        ValidateShape(Elements, nameof(elements));
    }

    /// <summary>
    /// Gets the alternating node and relationship elements in the path.
    /// </summary>
    public IReadOnlyList<PatternElement> Elements { get; }

    private static void ValidateShape(IReadOnlyList<PatternElement> elements, string parameterName)
    {
        if (elements[0] is not NodePattern || elements[^1] is not NodePattern)
        {
            throw new ArgumentException("A path pattern must start and end with a node pattern.", parameterName);
        }

        for (var i = 0; i < elements.Count; i++)
        {
            var hasExpectedType = i % 2 == 0
                ? elements[i] is NodePattern
                : elements[i] is RelationshipPattern;

            if (!hasExpectedType)
            {
                throw new ArgumentException("A path pattern must alternate node and relationship patterns.", parameterName);
            }
        }
    }
}

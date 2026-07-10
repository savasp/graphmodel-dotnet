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

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes the projection shape of a query.
/// </summary>
public sealed record ProjectionShape
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionShape"/> record.
    /// </summary>
    /// <param name="kind">The projection kind.</param>
    /// <param name="selector">The normalized selector lambda expression, if the projection came from one.</param>
    public ProjectionShape(ProjectionKind kind, LambdaExpression? selector)
    {
        QueryModelGuard.RequireDefinedEnum(kind, nameof(kind));

        if (kind != ProjectionKind.Identity && selector is null)
        {
            throw new ArgumentNullException(nameof(selector), "Non-identity projections require a selector.");
        }

        if (selector?.ReturnType == typeof(void))
        {
            throw new ArgumentException("A projection selector must return a value.", nameof(selector));
        }

        Kind = kind;
        Selector = selector;
    }

    /// <summary>
    /// Gets the projection kind.
    /// </summary>
    public ProjectionKind Kind { get; }

    /// <summary>
    /// Gets the normalized selector lambda expression, if the projection came from one.
    /// </summary>
    public LambdaExpression? Selector { get; }
}

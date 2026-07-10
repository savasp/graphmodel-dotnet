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
/// Describes a predicate expression and the alias it applies to, if one is known.
/// </summary>
public sealed record PredicateFragment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PredicateFragment"/> record.
    /// </summary>
    /// <param name="predicate">The normalized predicate lambda expression.</param>
    /// <param name="alias">The provider-neutral alias associated with the predicate, if known.</param>
    public PredicateFragment(LambdaExpression predicate, string? alias)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        QueryModelGuard.RequireNullOrNotWhiteSpace(alias, nameof(alias));

        if (predicate.ReturnType != typeof(bool))
        {
            throw new ArgumentException("A predicate lambda must return Boolean.", nameof(predicate));
        }

        Predicate = predicate;
        Alias = alias;
    }

    /// <summary>
    /// Gets the normalized predicate lambda expression.
    /// </summary>
    public LambdaExpression Predicate { get; }

    /// <summary>
    /// Gets the provider-neutral alias associated with the predicate, if known.
    /// </summary>
    public string? Alias { get; }
}

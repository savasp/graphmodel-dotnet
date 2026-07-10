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

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes one ordering key.
/// </summary>
public sealed record OrderingKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderingKey"/> record.
    /// </summary>
    /// <param name="keySelector">The normalized ordering key selector.</param>
    /// <param name="descending">A value indicating whether the key is sorted descending.</param>
    public OrderingKey(LambdaExpression keySelector, bool descending)
        : this(keySelector, descending, alias: null)
    {
    }

    /// <summary>
    /// Initializes an ordering key associated with a semantic query scope.
    /// </summary>
    /// <param name="keySelector">The normalized ordering key selector.</param>
    /// <param name="descending">A value indicating whether the key is sorted descending.</param>
    /// <param name="alias">The semantic scope associated with the key, if known.</param>
    public OrderingKey(LambdaExpression keySelector, bool descending, string? alias)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        QueryModelGuard.RequireNullOrNotWhiteSpace(alias, nameof(alias));

        if (keySelector.ReturnType == typeof(void))
        {
            throw new ArgumentException("An ordering key selector must return a value.", nameof(keySelector));
        }

        KeySelector = keySelector;
        Descending = descending;
        Alias = alias;
    }

    /// <summary>
    /// Gets the normalized ordering key selector.
    /// </summary>
    public LambdaExpression KeySelector { get; }

    /// <summary>
    /// Gets a value indicating whether the key is sorted descending.
    /// </summary>
    public bool Descending { get; }

    /// <summary>Gets the semantic scope associated with the key, if known.</summary>
    public string? Alias { get; }
}

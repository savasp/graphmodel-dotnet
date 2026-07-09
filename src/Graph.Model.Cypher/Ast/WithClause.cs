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
/// Represents a Cypher WITH clause.
/// </summary>
public sealed record WithClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithClause"/> class.
    /// </summary>
    /// <param name="items">The projected items.</param>
    /// <param name="distinct">Whether the projection is distinct.</param>
    public WithClause(IReadOnlyList<ReturnItem> items, bool distinct)
    {
        Items = ArgumentValidation.RequiredList(items, nameof(items));
        Distinct = distinct;
    }

    /// <summary>
    /// Gets the projected items.
    /// </summary>
    public IReadOnlyList<ReturnItem> Items { get; }

    /// <summary>
    /// Gets a value indicating whether the projection is distinct.
    /// </summary>
    public bool Distinct { get; }
}

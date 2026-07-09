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

namespace Cvoya.Graph.Model.Querying;

/// <summary>
/// Describes the provider-independent meaning of a graph query.
/// </summary>
/// <remarks>
/// This model is the level-1 semantic intermediate representation: it captures what the query
/// asks for without choosing a provider-specific execution plan or query language.
/// </remarks>
public sealed record GraphQueryModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryModel"/> record.
    /// </summary>
    /// <param name="root">The query root.</param>
    /// <param name="predicates">Predicates applied to the root scope.</param>
    /// <param name="traversal">Traversal steps applied after root predicates.</param>
    /// <param name="projection">The projection shape, or <see langword="null"/> for the current element.</param>
    /// <param name="ordering">Ordering keys applied to the current element.</param>
    /// <param name="paging">Skip/take paging information.</param>
    /// <param name="terminal">The terminal operation or terminal modifier for the query.</param>
    public GraphQueryModel(
        QueryRoot root,
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<TraversalStep> traversal,
        ProjectionShape? projection,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        TerminalOperation terminal)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Predicates = QueryModelGuard.CopyRequiredList(predicates, nameof(predicates));
        Traversal = QueryModelGuard.CopyRequiredList(traversal, nameof(traversal));
        Projection = projection;
        Ordering = QueryModelGuard.CopyRequiredList(ordering, nameof(ordering));
        Paging = paging ?? throw new ArgumentNullException(nameof(paging));
        QueryModelGuard.RequireDefinedEnum(terminal, nameof(terminal));
        Terminal = terminal;
    }

    /// <summary>
    /// Gets the query root.
    /// </summary>
    public QueryRoot Root { get; }

    /// <summary>
    /// Gets predicates applied to the root scope.
    /// </summary>
    public IReadOnlyList<PredicateFragment> Predicates { get; }

    /// <summary>
    /// Gets traversal steps applied after the root predicates.
    /// </summary>
    public IReadOnlyList<TraversalStep> Traversal { get; }

    /// <summary>
    /// Gets the projection shape, or <see langword="null"/> when the current element is returned.
    /// </summary>
    public ProjectionShape? Projection { get; }

    /// <summary>
    /// Gets ordering keys applied to the current element.
    /// </summary>
    public IReadOnlyList<OrderingKey> Ordering { get; }

    /// <summary>
    /// Gets skip/take paging information.
    /// </summary>
    public Paging Paging { get; }

    /// <summary>
    /// Gets the terminal operation or terminal modifier for the query.
    /// </summary>
    public TerminalOperation Terminal { get; }
}

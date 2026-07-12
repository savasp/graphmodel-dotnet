// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Describes sequence operations that must run after the query's primary paging window.
/// </summary>
/// <remarks>
/// The primary query model fields describe the initial sequence stage. This continuation preserves
/// LINQ operator order when filtering, deduplication, or ordering textually follows
/// <see cref="GraphQueryModel.Paging"/>. Its own paging window, when present, runs last.
/// </remarks>
public sealed record PostPagingStage
{
    /// <summary>Initializes a post-paging sequence stage.</summary>
    /// <param name="predicates">Predicates applied to the paged sequence.</param>
    /// <param name="ordering">Ordering keys applied to the paged sequence.</param>
    /// <param name="paging">Optional paging applied after this stage's other operations.</param>
    /// <param name="distinct">Whether the paged sequence is deduplicated.</param>
    public PostPagingStage(
        IReadOnlyList<PredicateFragment> predicates,
        IReadOnlyList<OrderingKey> ordering,
        Paging paging,
        bool distinct)
    {
        Predicates = QueryModelGuard.CopyRequiredList(predicates, nameof(predicates));
        Ordering = QueryModelGuard.CopyRequiredList(ordering, nameof(ordering));
        Paging = paging ?? throw new ArgumentNullException(nameof(paging));
        Distinct = distinct;
    }

    /// <summary>Gets predicates applied to the primary paged sequence.</summary>
    public IReadOnlyList<PredicateFragment> Predicates { get; }

    /// <summary>Gets ordering keys applied to the primary paged sequence.</summary>
    public IReadOnlyList<OrderingKey> Ordering { get; }

    /// <summary>Gets paging applied after this stage's other operations.</summary>
    public Paging Paging { get; }

    /// <summary>Gets a value indicating whether the primary paged sequence is deduplicated.</summary>
    public bool Distinct { get; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher ORDER BY clause.
/// </summary>
public sealed record OrderByClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderByClause"/> class.
    /// </summary>
    /// <param name="items">The ordered items.</param>
    public OrderByClause(IReadOnlyList<OrderByItem> items)
    {
        Items = ArgumentValidation.RequiredList(items, nameof(items));
    }

    /// <summary>
    /// Gets the ordered items.
    /// </summary>
    public IReadOnlyList<OrderByItem> Items { get; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

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

    private WithClause()
    {
        Items = [];
        Distinct = false;
        Wildcard = true;
    }

    /// <summary>
    /// Gets a clause that carries every bound variable forward unchanged (<c>WITH *</c>).
    /// </summary>
    /// <remarks>
    /// A bare <c>WHERE</c> attaches to the immediately preceding <c>MATCH</c>/<c>OPTIONAL MATCH</c>;
    /// inserting this clause turns the subsequent <c>WHERE</c> into a row filter instead.
    /// </remarks>
    public static WithClause All { get; } = new();

    /// <summary>
    /// Gets the projected items. Empty when <see cref="Wildcard"/> is set.
    /// </summary>
    public IReadOnlyList<ReturnItem> Items { get; }

    /// <summary>
    /// Gets a value indicating whether the projection is distinct.
    /// </summary>
    public bool Distinct { get; }

    /// <summary>
    /// Gets a value indicating whether the clause projects all bound variables (<c>WITH *</c>).
    /// </summary>
    public bool Wildcard { get; }
}

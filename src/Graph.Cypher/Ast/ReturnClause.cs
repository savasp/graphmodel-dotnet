// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher RETURN clause.
/// </summary>
public sealed record ReturnClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnClause"/> class.
    /// </summary>
    /// <param name="items">The returned items.</param>
    /// <param name="distinct">Whether the returned items are distinct.</param>
    public ReturnClause(IReadOnlyList<ReturnItem> items, bool distinct)
    {
        Items = ArgumentValidation.RequiredList(items, nameof(items));
        Distinct = distinct;
    }

    /// <summary>
    /// Gets the returned items.
    /// </summary>
    public IReadOnlyList<ReturnItem> Items { get; }

    /// <summary>
    /// Gets a value indicating whether the returned items are distinct.
    /// </summary>
    public bool Distinct { get; }
}

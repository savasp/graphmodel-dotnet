// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

using Cvoya.Graph.Cypher.Internal;

/// <summary>Represents a Cypher SET clause containing mapped property assignments.</summary>
public sealed record SetClause : ICypherClause
{
    /// <summary>Initializes a Cypher SET clause.</summary>
    /// <param name="items">The property assignments.</param>
    public SetClause(IReadOnlyList<SetItem> items) =>
        Items = ArgumentValidation.RequiredList(items, nameof(items));

    /// <summary>Gets the property assignments.</summary>
    public IReadOnlyList<SetItem> Items { get; }
}

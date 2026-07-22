// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents null-aware membership in a simple collection.</summary>
public sealed record CollectionContainsExpression : CypherExpression
{
    /// <summary>Initializes null-aware collection membership.</summary>
    public CollectionContainsExpression(CypherExpression collection, CypherExpression item)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>Gets the collection expression.</summary>
    public CypherExpression Collection { get; }

    /// <summary>Gets the item expression.</summary>
    public CypherExpression Item { get; }
}

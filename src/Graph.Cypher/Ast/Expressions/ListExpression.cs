// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents a Cypher list expression.</summary>
public sealed record ListExpression : CypherExpression
{
    /// <summary>Initializes a list expression.</summary>
    public ListExpression(IReadOnlyList<CypherExpression> items)
    {
        Items = ArgumentValidation.List(items, nameof(items));
    }

    /// <summary>Gets the list items.</summary>
    public IReadOnlyList<CypherExpression> Items { get; }
}

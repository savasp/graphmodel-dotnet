// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents a Cypher map expression.</summary>
public sealed record MapExpression : CypherExpression
{
    /// <summary>Initializes a map expression.</summary>
    public MapExpression(IReadOnlyList<MapEntry> entries)
    {
        Entries = ArgumentValidation.List(entries, nameof(entries));
    }

    /// <summary>Gets the map entries.</summary>
    public IReadOnlyList<MapEntry> Entries { get; }
}

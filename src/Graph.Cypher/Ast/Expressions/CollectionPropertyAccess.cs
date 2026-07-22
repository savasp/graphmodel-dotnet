// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents logical access to a stored simple-collection property.</summary>
public sealed record CollectionPropertyAccess : CypherExpression
{
    /// <summary>Initializes collection property access.</summary>
    public CollectionPropertyAccess(CypherExpression target, string property, bool escape)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        Property = property;
        Escape = escape;
    }

    /// <summary>Gets the target expression.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the logical property name.</summary>
    public string Property { get; }

    /// <summary>Gets whether the logical name came from a dynamic accessor.</summary>
    public bool Escape { get; }
}

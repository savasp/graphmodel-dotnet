// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents access to an already-encoded physical property name.</summary>
public sealed record PhysicalPropertyAccess : CypherExpression
{
    /// <summary>Initializes physical property access.</summary>
    public PhysicalPropertyAccess(CypherExpression target, string property)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        Property = property;
    }

    /// <summary>Gets the target expression.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the physical property identifier.</summary>
    public string Property { get; }
}

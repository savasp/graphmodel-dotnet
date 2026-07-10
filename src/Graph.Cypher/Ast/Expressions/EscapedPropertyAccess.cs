// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents dynamic property access whose identifier must be escaped by the renderer.</summary>
public sealed record EscapedPropertyAccess : CypherExpression
{
    /// <summary>Initializes dynamic property access.</summary>
    public EscapedPropertyAccess(CypherExpression target, string property)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
    }

    /// <summary>Gets the target expression.</summary>
    public CypherExpression Target { get; }

    /// <summary>Gets the unescaped property identifier.</summary>
    public string Property { get; }
}

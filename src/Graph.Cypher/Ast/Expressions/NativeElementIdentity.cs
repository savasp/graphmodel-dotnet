// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Projects provider-native graph element identity for transaction-local command use.</summary>
public sealed record NativeElementIdentity : CypherExpression
{
    /// <summary>Initializes a native graph element identity expression.</summary>
    /// <param name="target">The node or relationship expression.</param>
    public NativeElementIdentity(CypherExpression target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    /// <summary>Gets the graph element whose native identity is projected.</summary>
    public CypherExpression Target { get; }
}

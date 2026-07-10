// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents property access on a Cypher expression.
/// </summary>
public sealed record PropertyAccess : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyAccess"/> class.
    /// </summary>
    /// <param name="target">The expression whose property is accessed.</param>
    /// <param name="property">The property name.</param>
    public PropertyAccess(CypherExpression target, string property)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
        Property = ArgumentValidation.RequiredName(property, nameof(property));
    }

    /// <summary>
    /// Gets the expression whose property is accessed.
    /// </summary>
    public CypherExpression Target { get; }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Property { get; }
}

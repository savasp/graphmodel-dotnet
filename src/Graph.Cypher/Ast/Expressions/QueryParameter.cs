// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a Cypher query parameter reference.
/// </summary>
public sealed record QueryParameter : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryParameter"/> class.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    public QueryParameter(string name)
    {
        Name = ArgumentValidation.RequiredName(name, nameof(name));
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }
}

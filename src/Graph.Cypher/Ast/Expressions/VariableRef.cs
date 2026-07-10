// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a reference to a Cypher variable.
/// </summary>
public sealed record VariableRef : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VariableRef"/> class.
    /// </summary>
    /// <param name="alias">The referenced alias.</param>
    public VariableRef(string alias)
    {
        Alias = ArgumentValidation.RequiredName(alias, nameof(alias));
    }

    /// <summary>
    /// Gets the referenced alias.
    /// </summary>
    public string Alias { get; }
}

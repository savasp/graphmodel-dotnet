// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>
/// Represents a Cypher function call expression.
/// </summary>
public sealed record FunctionCall : CypherExpression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionCall"/> class.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="arguments">The function arguments.</param>
    public FunctionCall(string name, IReadOnlyList<CypherExpression> arguments)
    {
        Name = ArgumentValidation.RequiredName(name, nameof(name));
        Arguments = ArgumentValidation.List(arguments, nameof(arguments));
    }

    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the function arguments.
    /// </summary>
    public IReadOnlyList<CypherExpression> Arguments { get; }
}

// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast;

namespace Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Represents a transformation or validation pass over a Cypher statement.
/// </summary>
public interface ICypherPass
{
    /// <summary>
    /// Runs the pass over the provided statement.
    /// </summary>
    /// <param name="input">The input statement.</param>
    /// <returns>The statement produced by the pass.</returns>
    CypherStatement Run(CypherStatement input);
}

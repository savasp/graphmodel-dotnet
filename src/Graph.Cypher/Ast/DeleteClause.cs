// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast;

using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Internal;

/// <summary>Represents a Cypher DELETE or DETACH DELETE clause.</summary>
public sealed record DeleteClause : ICypherClause
{
    /// <summary>Initializes a Cypher delete clause.</summary>
    /// <param name="targets">The bound variables to delete.</param>
    /// <param name="detach">Whether incident relationships are detached.</param>
    public DeleteClause(IReadOnlyList<VariableRef> targets, bool detach)
    {
        Targets = ArgumentValidation.RequiredList(targets, nameof(targets));
        Detach = detach;
    }

    /// <summary>Gets the variables to delete.</summary>
    public IReadOnlyList<VariableRef> Targets { get; }

    /// <summary>Gets whether incident relationships are detached.</summary>
    public bool Detach { get; }
}

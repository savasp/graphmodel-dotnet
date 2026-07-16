// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>Combines two complete query branches with <c>UNION</c> or <c>UNION ALL</c>.</summary>
public sealed record SetOperationClause : ICypherClause
{
    /// <summary>Initializes a set-operation clause.</summary>
    public SetOperationClause(
        IReadOnlyList<ICypherClause> first,
        IReadOnlyList<ICypherClause> second,
        bool preserveDuplicates)
    {
        First = ArgumentValidation.RequiredList(first, nameof(first));
        Second = ArgumentValidation.RequiredList(second, nameof(second));
        PreserveDuplicates = preserveDuplicates;
    }

    /// <summary>Gets the first query branch.</summary>
    public IReadOnlyList<ICypherClause> First { get; }

    /// <summary>Gets the second query branch.</summary>
    public IReadOnlyList<ICypherClause> Second { get; }

    /// <summary>Gets whether duplicate rows are preserved.</summary>
    public bool PreserveDuplicates { get; }
}

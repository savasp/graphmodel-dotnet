// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Defines pattern subquery result kinds.</summary>
public enum PatternSubqueryKind
{
    /// <summary>An existence predicate.</summary>
    Exists,

    /// <summary>A row count expression.</summary>
    Count,
}

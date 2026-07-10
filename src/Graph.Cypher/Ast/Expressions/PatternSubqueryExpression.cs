// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents an EXISTS or COUNT pattern subquery expression.</summary>
public sealed record PatternSubqueryExpression : CypherExpression
{
    /// <summary>Initializes a pattern subquery.</summary>
    public PatternSubqueryExpression(
        PatternSubqueryKind kind,
        PathPattern pattern,
        CypherExpression? predicate = null)
    {
        Kind = ArgumentValidation.DefinedEnum(kind, nameof(kind));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Predicate = predicate;
    }

    /// <summary>Gets the subquery kind.</summary>
    public PatternSubqueryKind Kind { get; }

    /// <summary>Gets the matched pattern.</summary>
    public PathPattern Pattern { get; }

    /// <summary>Gets the optional pattern predicate.</summary>
    public CypherExpression? Predicate { get; }
}

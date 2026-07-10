// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents a Cypher pattern comprehension.</summary>
public sealed record PatternComprehensionExpression : CypherExpression
{
    /// <summary>Initializes a pattern comprehension.</summary>
    public PatternComprehensionExpression(
        PathPattern pattern,
        CypherExpression projection,
        CypherExpression? predicate = null)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        Predicate = predicate;
    }

    /// <summary>Gets the pattern.</summary>
    public PathPattern Pattern { get; }

    /// <summary>Gets the projected expression.</summary>
    public CypherExpression Projection { get; }

    /// <summary>Gets the optional predicate.</summary>
    public CypherExpression? Predicate { get; }
}

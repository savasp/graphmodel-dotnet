// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher MATCH or OPTIONAL MATCH clause.
/// </summary>
public sealed record MatchClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatchClause"/> class.
    /// </summary>
    /// <param name="patterns">The path patterns matched by the clause.</param>
    /// <param name="optional">Whether this clause is an OPTIONAL MATCH.</param>
    public MatchClause(IReadOnlyList<PathPattern> patterns, bool optional)
    {
        Patterns = ArgumentValidation.RequiredList(patterns, nameof(patterns));
        Optional = optional;
    }

    /// <summary>
    /// Gets the path patterns matched by the clause.
    /// </summary>
    public IReadOnlyList<PathPattern> Patterns { get; }

    /// <summary>
    /// Gets a value indicating whether this clause is an OPTIONAL MATCH.
    /// </summary>
    public bool Optional { get; }
}

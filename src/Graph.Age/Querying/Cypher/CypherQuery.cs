// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher;

/// <summary>
/// Represents a Cypher query with its parameters and optional transaction context.
/// </summary>
/// <param name="Text">The rendered Cypher text.</param>
/// <param name="Parameters">The parameters referenced by <paramref name="Text"/>.</param>
/// <param name="GraphPathTypes">
/// When this query is a decomposed <c>TraversePaths</c> result (one row per hop, tagged with
/// <c>pathIndex</c>/<c>hopIndex</c> columns), the source/relationship/target types needed to
/// deserialize each hop; <see langword="null"/> for all other queries.
/// </param>
/// <param name="ProjectionColumns">The exact projected column names in rendered order.</param>
internal sealed record CypherQuery(
    string Text,
    IReadOnlyDictionary<string, object?> Parameters,
    (Type Source, Type Relationship, Type Target)? GraphPathTypes = null,
    IReadOnlyList<string>? ProjectionColumns = null)
{
    /// <summary>
    /// Creates an empty query.
    /// </summary>
    public static CypherQuery Empty { get; } = new(
        string.Empty,
        new Dictionary<string, object?>(),
        ProjectionColumns: []);
}

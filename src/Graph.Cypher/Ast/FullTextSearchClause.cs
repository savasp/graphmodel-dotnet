// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Querying;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a provider-neutral full-text search operation.
/// </summary>
public sealed record FullTextSearchClause : ICypherClause
{
    /// <summary>Initializes a full-text search operation.</summary>
    /// <param name="target">The graph entity category to search.</param>
    /// <param name="query">The query parameter reference.</param>
    /// <param name="alias">The result alias.</param>
    public FullTextSearchClause(SearchRootTarget target, QueryParameter query, string alias)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "The full-text search target is not defined.");
        }

        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        Target = target;
        Query = query;
        Alias = alias;
    }

    /// <summary>Gets the graph entity category to search.</summary>
    public SearchRootTarget Target { get; }

    /// <summary>Gets the parameter that contains the search query.</summary>
    public QueryParameter Query { get; }

    /// <summary>Gets the local result alias.</summary>
    public string Alias { get; }
}

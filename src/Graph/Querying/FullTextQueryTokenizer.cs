// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

/// <summary>
/// Provides the single provider-neutral definition of a full-text search "term": splitting a raw
/// query into whole-word tokens so every provider agrees on what a term is when lowering the shared
/// <see cref="SearchRoot"/> into its own engine syntax.
/// </summary>
internal static class FullTextQueryTokenizer
{
    /// <summary>
    /// Splits <paramref name="query"/> into search terms by breaking on any character that is not a
    /// letter or digit (<see cref="char.IsLetterOrDigit(char)"/>), lowercasing each token with the
    /// invariant culture and dropping empty tokens.
    /// </summary>
    /// <param name="query">The raw user query string.</param>
    /// <returns>The ordered list of non-empty, lowercased tokens; empty when the query has no terms.</returns>
    public static IReadOnlyList<string> Tokenize(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i < query.Length; i++)
        {
            if (char.IsLetterOrDigit(query[i]))
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                tokens.Add(query[start..i].ToLowerInvariant());
                start = -1;
            }
        }

        if (start >= 0)
        {
            tokens.Add(query[start..].ToLowerInvariant());
        }

        return tokens;
    }
}

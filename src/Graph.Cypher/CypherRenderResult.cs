// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections.ObjectModel;

namespace Cvoya.Graph.Cypher;

/// <summary>
/// Contains rendered Cypher together with its transport parameters and exact projection columns.
/// </summary>
public sealed record CypherRenderResult
{
    /// <summary>Initializes a rendered Cypher result.</summary>
    /// <param name="text">The rendered Cypher text.</param>
    /// <param name="parameters">The parameter values referenced by the text.</param>
    /// <param name="projectionColumns">The projected column names, preserving their rendered casing.</param>
    public CypherRenderResult(
        string text,
        IReadOnlyDictionary<string, object?> parameters,
        IReadOnlyList<string> projectionColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(projectionColumns);

        Text = text;
        Parameters = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(parameters, StringComparer.Ordinal));
        ProjectionColumns = Array.AsReadOnly(projectionColumns.Select(column =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(column);
            return column;
        }).ToArray());
    }

    /// <summary>Gets the rendered Cypher text.</summary>
    public string Text { get; }

    /// <summary>Gets the parameter values referenced by <see cref="Text"/>.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Gets the exact projected column names.</summary>
    public IReadOnlyList<string> ProjectionColumns { get; }
}

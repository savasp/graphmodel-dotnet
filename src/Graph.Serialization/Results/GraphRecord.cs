// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using System.Collections.ObjectModel;

namespace Cvoya.Graph.Serialization.Results;

/// <summary>Represents one immutable provider-neutral result record.</summary>
public sealed record GraphRecord
{
    /// <summary>Initializes a result record from named wire values.</summary>
    /// <param name="values">The named values in the record.</param>
    public GraphRecord(IReadOnlyDictionary<string, GraphValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = new Dictionary<string, GraphValue>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            copy.Add(key, value ?? throw new ArgumentException(
                "Graph records cannot contain null values; use GraphValue.Scalar(null).",
                nameof(values)));
        }

        this.values = new ReadOnlyDictionary<string, GraphValue>(copy);
    }

    private readonly ReadOnlyDictionary<string, GraphValue> values;

    /// <summary>Gets the record's named wire values.</summary>
    public IReadOnlyDictionary<string, GraphValue> Values => values;

    /// <summary>Gets the record's column names.</summary>
    public IReadOnlyCollection<string> Keys => values.Keys;

    /// <summary>Gets a wire value by column name.</summary>
    public GraphValue this[string key] => Values[key];
}

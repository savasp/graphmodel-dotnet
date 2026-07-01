// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System.Collections.ObjectModel;
using Cvoya.Graph.Model.Age.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Centralized store for query parameters emitted while translating LINQ expressions to Cypher.
/// </summary>
internal sealed class QueryParameterStore
{
    private readonly ILogger<QueryParameterStore> logger;
    private readonly Dictionary<string, object?> parameters = new();
    // O(1) value-to-name reverse lookup for parameter deduplication.
    // Null values are tracked separately since Dictionary<TKey,TValue> has a 'notnull' constraint on TKey.
    private readonly Dictionary<object, string> valueToName = new(EqualityComparer<object>.Default);
    private string? nullValueName;

    public QueryParameterStore(ILoggerFactory? loggerFactory)
    {
        logger = loggerFactory?.CreateLogger<QueryParameterStore>() ?? NullLogger<QueryParameterStore>.Instance;
    }

    /// <summary>
    /// Adds a parameter value and returns the Cypher reference (e.g. "$param_0").
    /// Uses O(1) dictionary lookup for deduplication instead of O(n) linear scan.
    /// </summary>
    public string Add(object? value)
    {
        var convertedValue = AgeSerializationBridge.ToAgeValue(value);

        // O(1) lookup: nulls tracked separately, non-null via dictionary
        if (convertedValue is null)
        {
            if (nullValueName is not null)
            {
                logger.LogDebug("Reusing parameter {ParameterName}", nullValueName);
                return $"${nullValueName}";
            }
        }
        else if (valueToName.TryGetValue(convertedValue, out var existingName))
        {
            logger.LogDebug("Reusing parameter {ParameterName}", existingName);
            return $"${existingName}";
        }

        var paramName = $"param_{parameters.Count}";
        parameters[paramName] = convertedValue;

        if (convertedValue is null)
            nullValueName = paramName;
        else
            valueToName[convertedValue] = paramName;

        logger.LogDebug("Added parameter {ParameterName} ({ParameterType})", paramName, convertedValue?.GetType().Name);
        logger.LogTrace("Added parameter {ParameterName} = {ParameterValue}", paramName, convertedValue);
        return $"${paramName}";
    }

    /// <summary>
    /// Returns an immutable snapshot of all collected parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Snapshot()
        => new ReadOnlyDictionary<string, object?>(parameters);
}

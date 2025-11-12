// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cvoya.Graph.Model.Age.Core.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Centralized store for query parameters emitted while translating LINQ expressions to Cypher.
/// This replaces the ad-hoc storage that previously lived on the legacy query builder.
/// </summary>
internal sealed class QueryParameterStore
{
    private readonly ILogger<QueryParameterStore> logger;
    private readonly Dictionary<string, object?> parameters = new();

    public QueryParameterStore(ILoggerFactory? loggerFactory)
    {
        logger = loggerFactory?.CreateLogger<QueryParameterStore>() ?? NullLogger<QueryParameterStore>.Instance;
    }

    /// <summary>
    /// Adds a parameter value and returns the Cypher reference (e.g. "$param_0").
    /// Reuses existing parameter names when the value matches an earlier entry.
    /// </summary>
    public string Add(object? value)
    {
        var convertedValue = AgeSerializationBridge.ToAgeValue(value);

        foreach (var parameter in parameters)
        {
            if (Equals(parameter.Value, convertedValue))
            {
                logger.LogDebug("Reusing parameter {ParameterName}", parameter.Key);
                return $"${parameter.Key}";
            }
        }

        var paramName = $"param_{parameters.Count}";
        parameters[paramName] = convertedValue;
        logger.LogDebug("Added parameter {ParameterName} = {ParameterValue}", paramName, convertedValue);
        return $"${paramName}";
    }

    /// <summary>
    /// Returns an immutable snapshot of all collected parameters.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Snapshot()
        => new ReadOnlyDictionary<string, object?>(parameters);
}

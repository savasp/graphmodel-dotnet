// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Planning;

internal sealed class CypherParameterRegistry
{
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    public QueryParameter Add(object? value)
    {
        var normalized = Normalize(value);
        foreach (var (name, existing) in _parameters)
        {
            if (Equals(existing, normalized))
            {
                return new QueryParameter(name);
            }
        }

        var parameterName = $"p{_parameters.Count}";
        _parameters.Add(parameterName, normalized);
        return new QueryParameter(parameterName);
    }

    private static object? Normalize(object? value)
    {
        return value is Enum enumValue
            ? Convert.ChangeType(
                enumValue,
                Enum.GetUnderlyingType(enumValue.GetType()),
                System.Globalization.CultureInfo.InvariantCulture)
            : value;
    }
}

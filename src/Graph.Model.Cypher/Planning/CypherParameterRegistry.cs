// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Cvoya.Graph.Model.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Model.Cypher.Planning;

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

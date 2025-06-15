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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher;

using Cvoya.Graph.Model.Neo4j.Serialization;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class ResultMaterializer
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<ResultMaterializer> _logger;

    public ResultMaterializer(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<ResultMaterializer>() ?? NullLogger<ResultMaterializer>.Instance;
    }

    public Task<List<T>> MaterializeAsync<T>(
        List<EntityInfo> entityInfos,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        var targetType = typeof(T);

        foreach (var entityInfo in entityInfos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Use EntityFactory for types it can serialize/deserialize
            if (_entityFactory.CanDeserialize(targetType))
            {
                var materializedObject = _entityFactory.Deserialize(entityInfo);
                results.Add((T)materializedObject);
            }
            else
            {
                // Use reflection for types EntityFactory can't handle (projections)
                var materializedObject = MaterializeUsingReflection<T>(entityInfo);
                results.Add(materializedObject);
            }
        }

        return Task.FromResult(results);
    }

    private T MaterializeUsingReflection<T>(EntityInfo entityInfo)
    {
        var targetType = typeof(T);

        // For projections, we're dealing with regular .NET object construction
        // No "simple" vs "complex" - just normal object materialization

        // Handle value types and basic types that can be directly converted
        if (entityInfo.SimpleProperties.Count == 1 &&
            (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(DateTime) || targetType.IsEnum))
        {
            var singleProperty = entityInfo.SimpleProperties.First().Value;
            if (singleProperty.Value is SimpleValue simpleValue)
            {
                return (T)Convert.ChangeType(simpleValue.Object, targetType);
            }
        }

        // Handle regular object construction using reflection
        var constructorInfo = targetType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = constructorInfo.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? $"param{i}";

            // Try to find matching property in EntityInfo
            if (entityInfo.SimpleProperties.TryGetValue(paramName, out var property) &&
                property.Value is SimpleValue simpleValue)
            {
                values[i] = ConvertValue(simpleValue.Object, param.ParameterType);
            }
            else
            {
                // Try case-insensitive match
                var matchingProperty = entityInfo.SimpleProperties
                    .FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase));

                if (matchingProperty.Value.Value is SimpleValue matchedValue)
                {
                    values[i] = ConvertValue(matchedValue.Object, param.ParameterType);
                }
                else
                {
                    values[i] = param.HasDefaultValue ? param.DefaultValue : null;
                }
            }
        }

        return (T)Activator.CreateInstance(targetType, values)!;
    }

    private object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        // Handle Neo4j specific types
        value = SerializationBridge.FromNeo4jValue(value, targetType)
            ?? throw new InvalidOperationException(
                $"Cannot convert value of type {value.GetType()} to target type {targetType}");

        // Handle type conversion
        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, value);

        return Convert.ChangeType(value, targetType);
    }
}
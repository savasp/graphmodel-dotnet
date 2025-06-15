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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Execution;

using System.Collections;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class ResultMaterializer
{
    private readonly EntityFactory _entityFactory;
    private readonly CypherResultProcessor _resultProcessor;
    private readonly ILogger<ResultMaterializer> _logger;

    public ResultMaterializer(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<ResultMaterializer>() ?? NullLogger<ResultMaterializer>.Instance;
        _resultProcessor = new CypherResultProcessor(_entityFactory, loggerFactory);
    }

    public async Task<T?> MaterializeAsync<T>(
        List<IRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (!records.Any())
            return default(T);

        var targetType = typeof(T);
        var elementType = Helpers.GetTargetTypeIfCollection(targetType);
        var isCollectionType = targetType != elementType;

        // Let the processor handle all the record processing logic
        var entityInfos = await _resultProcessor.ProcessAsync(records, elementType, cancellationToken);

        if (isCollectionType)
        {
            // Materialize all elements into a collection
            var elements = entityInfos.Select(entityInfo => MaterializeSingleElement(entityInfo, elementType)).ToList();
            return ConvertToCollectionType<T>(elements, elementType);
        }
        else
        {
            // Materialize single element
            var firstEntityInfo = entityInfos.FirstOrDefault();
            if (firstEntityInfo is null)
                return default(T);

            return (T?)MaterializeSingleElement(firstEntityInfo, elementType);
        }
    }

    private object? MaterializeSingleElement(EntityInfo entityInfo, Type elementType)
    {
        // Simple delegation - let EntityFactory handle entities, everything else is already processed
        if (_entityFactory.CanDeserialize(elementType))
        {
            return _entityFactory.Deserialize(entityInfo);
        }
        else
        {
            // For projections, the CypherResultProcessor already did the heavy lifting
            // We just need to create the object from the processed EntityInfo
            return CreateObjectFromEntityInfo(entityInfo, elementType);
        }
    }

    private object? CreateObjectFromEntityInfo(EntityInfo entityInfo, Type targetType)
    {
        // Handle simple types - should have exactly one property
        if (targetType.IsPrimitive || targetType == typeof(string) ||
            targetType == typeof(DateTime) || targetType.IsEnum)
        {
            if (entityInfo.SimpleProperties.Count == 1)
            {
                var singleProperty = entityInfo.SimpleProperties.First().Value;
                if (singleProperty.Value is SimpleValue simpleValue)
                {
                    return Convert.ChangeType(simpleValue.Object, targetType);
                }
            }
            return GetDefaultValue(targetType);
        }

        // Handle complex types - use constructor matching
        return CreateComplexObject(entityInfo, targetType);
    }

    private object? CreateComplexObject(EntityInfo entityInfo, Type targetType)
    {
        var constructors = targetType.GetConstructors();
        if (!constructors.Any())
        {
            throw new InvalidOperationException($"Type {targetType.Name} has no accessible constructors");
        }

        var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
        var parameters = constructor.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? $"param{i}";

            // Try to find matching property (case-insensitive)
            var matchingProperty = entityInfo.SimpleProperties
                .FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase));

            if (matchingProperty.Value?.Value is SimpleValue simpleValue)
            {
                values[i] = ConvertToParameterType(simpleValue.Object, param.ParameterType);
            }
            else
            {
                values[i] = param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType);
            }
        }

        return Activator.CreateInstance(targetType, values);
    }

    private static object? ConvertToParameterType(object? value, Type targetType)
    {
        if (value is null)
            return GetDefaultValue(targetType);

        if (targetType.IsAssignableFrom(value.GetType()))
            return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, value);

        return Convert.ChangeType(value, targetType);
    }

    private static object? GetDefaultValue(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;

    private static T ConvertToCollectionType<T>(List<object?> items, Type elementType)
    {
        var targetType = typeof(T);

        // Handle arrays
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return (T)(object)array;
        }

        // Handle generic collections
        if (targetType.IsGenericType)
        {
            var genericTypeDefinition = targetType.GetGenericTypeDefinition();

            if (genericTypeDefinition == typeof(List<>) ||
                genericTypeDefinition == typeof(IList<>) ||
                genericTypeDefinition == typeof(ICollection<>) ||
                genericTypeDefinition == typeof(IEnumerable<>))
            {
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var item in items.Where(i => i is not null))
                {
                    list.Add(item);
                }
                return (T)list;
            }
        }

        throw new NotSupportedException($"Cannot convert results to collection type {targetType}");
    }
}
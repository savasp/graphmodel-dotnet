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
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;
using Cvoya.Graph.Model.Neo4j.Serialization;
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
        var targetType = typeof(T);
        var elementType = Helpers.GetTargetTypeIfCollection(targetType);
        var isCollectionType = targetType != elementType;

        _logger.LogDebug("MaterializeAsync: TargetType={TargetType}, ElementType={ElementType}, IsCollectionType={IsCollectionType}, RecordCount={RecordCount}",
            targetType.Name, elementType.Name, isCollectionType, records.Count);

        // Handle empty results consistently
        if (!records.Any())
        {
            return isCollectionType
                ? ConvertToCollectionType<T>([], elementType)
                : default;
        }

        // Process records to EntityInfo objects
        var entityInfos = await _resultProcessor.ProcessAsync(records, elementType, cancellationToken);
        _logger.LogDebug("MaterializeAsync: ProcessAsync returned {EntityInfoCount} entity infos", entityInfos.Count);

        var result = isCollectionType
            ? MaterializeAsCollection<T>(entityInfos, elementType)
            : MaterializeSingleElement<T>(entityInfos.FirstOrDefault(), elementType);

        _logger.LogDebug("MaterializeAsync: Final result type={ResultType}", result?.GetType().Name ?? "null");
        if (result is IEnumerable enumerable && !(result is string))
        {
            var count = enumerable.Cast<object>().Count();
            _logger.LogDebug("MaterializeAsync: Collection result contains {Count} items", count);
        }

        return result;
    }

    private T MaterializeAsCollection<T>(List<EntityInfo> entityInfos, Type elementType)
    {
        _logger.LogDebug("MaterializeAsCollection: Processing {EntityInfoCount} entity infos for element type {ElementType}",
            entityInfos.Count, elementType.Name);

        var elements = entityInfos
            .Select(entityInfo => MaterializeSingleElement<object>(entityInfo, elementType))
            .Where(item => item is not null)
            .ToList();

        _logger.LogDebug("MaterializeAsCollection: Materialized {ElementCount} non-null elements", elements.Count);

        var result = ConvertToCollectionType<T>(elements!, elementType);
        _logger.LogDebug("MaterializeAsCollection: Converted to collection type {ResultType}", result?.GetType().Name ?? "null");

        return result;
    }

    private TResult? MaterializeSingleElement<TResult>(EntityInfo? entityInfo, Type elementType)
    {
        if (entityInfo is null)
            return default(TResult);

        // Check if we should use the actual type from EntityInfo instead of elementType
        var typeToDeserialize = entityInfo.ActualType ?? elementType;
        var canDeserialize = _entityFactory.CanDeserialize(typeToDeserialize);

        _logger.LogDebug("MaterializeSingleElement: ElementType={ElementType}, ActualType={ActualType}, CanDeserialize={CanDeserialize}",
            elementType.Name, entityInfo.ActualType?.Name ?? "null", canDeserialize);

        var result = canDeserialize
            ? _entityFactory.Deserialize(entityInfo)
            : CreateObjectFromEntityInfo(entityInfo, elementType);

        return result is null ? default(TResult) : (TResult)result;
    }

    private object? CreateObjectFromEntityInfo(EntityInfo entityInfo, Type targetType)
    {
        // Handle path segments specially
        if (IsPathSegmentType(targetType))
        {
            return CreatePathSegmentFromEntityInfo(entityInfo, targetType);
        }

        // Handle simple value projections
        if (GraphDataModel.IsSimple(targetType))
        {
            return CreateSimpleValue(entityInfo, targetType);
        }

        // Handle complex object construction
        return CreateComplexObject(entityInfo, targetType);
    }

    private object CreatePathSegmentFromEntityInfo(EntityInfo entityInfo, Type interfaceType)
    {
        var genericArgs = interfaceType.GetGenericArguments();
        var concreteType = typeof(GraphPathSegment<,,>).MakeGenericType(genericArgs);

        // Extract the three components from the path segment EntityInfo
        var startNodeEntityInfo = GetComplexProperty(entityInfo, "StartNode");
        var relationshipEntityInfo = GetComplexProperty(entityInfo, "Relationship");
        var endNodeEntityInfo = GetComplexProperty(entityInfo, "EndNode");

        // Materialize each component
        var startNode = MaterializeSingleElement<object>(startNodeEntityInfo, genericArgs[0]);
        var relationship = MaterializeSingleElement<object>(relationshipEntityInfo, genericArgs[1]);
        var endNode = MaterializeSingleElement<object>(endNodeEntityInfo, genericArgs[2]);

        // Find and invoke the constructor
        var constructor = concreteType.GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Length == 3)
            ?? throw new InvalidOperationException($"No 3-parameter constructor found for {concreteType.Name}");

        return constructor.Invoke([startNode, relationship, endNode]);
    }

    private EntityInfo? GetComplexProperty(EntityInfo entityInfo, string propertyName)
    {
        return entityInfo.ComplexProperties.TryGetValue(propertyName, out var property)
            && property.Value is EntityInfo nestedEntityInfo
            ? nestedEntityInfo
            : null;
    }

    private object? CreateSimpleValue(EntityInfo entityInfo, Type targetType)
    {
        if (entityInfo.SimpleProperties.Count == 1)
        {
            var singleProperty = entityInfo.SimpleProperties.First().Value;
            if (singleProperty.Value is SimpleValue simpleValue)
            {
                return ConvertValueToTargetType(simpleValue.Object, targetType);
            }
        }

        return GetDefaultValue(targetType);
    }

    private static object? ConvertValueToTargetType(object? value, Type targetType)
    {
        // Delegate all conversions to SerializationBridge for consistency
        return SerializationBridge.FromNeo4jValue(value, targetType);
    }

    private object? CreateComplexObject(EntityInfo entityInfo, Type targetType)
    {
        // Use the actual type from EntityInfo instead of the target type
        // This allows interfaces like IRelationship to be materialized as their concrete types (e.g., Knows, Friend)
        var typeToInstantiate = entityInfo.ActualType ?? targetType;

        _logger.LogDebug("CreateComplexObject: Creating {TypeName} from EntityInfo with {SimpleCount} simple properties, {ComplexCount} complex properties",
            typeToInstantiate.Name, entityInfo.SimpleProperties.Count, entityInfo.ComplexProperties.Count);

        _logger.LogDebug("CreateComplexObject: Simple properties: [{SimpleProps}]",
            string.Join(", ", entityInfo.SimpleProperties.Select(kvp => $"{kvp.Key}={kvp.Value.Value}")));
        _logger.LogDebug("CreateComplexObject: Complex properties: [{ComplexProps}]",
            string.Join(", ", entityInfo.ComplexProperties.Select(kvp => $"{kvp.Key}={kvp.Value.Value?.GetType().Name}")));

        var constructors = typeToInstantiate.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        if (constructors.Count == 0)
            throw new InvalidOperationException($"Type {typeToInstantiate.Name} has no accessible constructors");

        // Try each constructor until we find one that works
        foreach (var constructor in constructors)
        {
            try
            {
                var parameters = constructor.GetParameters();
                var values = new object?[parameters.Length];

                _logger.LogDebug("CreateComplexObject: Trying constructor with {ParamCount} parameters: [{Params}]",
                    parameters.Length, string.Join(", ", parameters.Select(p => $"{p.Name}:{p.ParameterType.Name}")));

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name ?? $"param{i}";
                    var matchingProperty = FindPropertyInEntityInfo(entityInfo, paramName);

                    _logger.LogDebug("CreateComplexObject: Parameter {ParamName} -> Property {PropertyFound} (Type: {PropertyType})",
                        paramName, matchingProperty != null ? "FOUND" : "NOT_FOUND",
                        matchingProperty?.Value?.GetType().Name ?? "null");

                    values[i] = matchingProperty?.Value switch
                    {
                        SimpleValue simpleValue => ConvertToParameterType(simpleValue.Object, param.ParameterType),
                        EntityInfo complexEntityInfo => MaterializeSingleElement<object>(complexEntityInfo, param.ParameterType),
                        _ => param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType)
                    };

                    _logger.LogDebug("CreateComplexObject: Parameter {ParamName} set to: {Value}",
                        paramName, values[i]?.ToString() ?? "null");
                }

                // Use constructor.Invoke instead of Activator.CreateInstance to avoid ambiguity
                var result = constructor.Invoke(values);
                _logger.LogDebug("CreateComplexObject: Successfully created {TypeName} instance", typeToInstantiate.Name);
                return result;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is AmbiguousMatchException || ex is TargetParameterCountException)
            {
                _logger.LogDebug("CreateComplexObject: Constructor failed: {Exception}", ex.Message);
                // Try the next constructor
                continue;
            }
        }

        throw new InvalidOperationException($"Could not find a suitable constructor for type {typeToInstantiate.Name}");
    }


    private static Property? FindPropertyInEntityInfo(EntityInfo entityInfo, string paramName)
    {
        // Try exact matches first
        if (entityInfo.SimpleProperties.TryGetValue(paramName, out var simpleProperty))
            return simpleProperty;

        if (entityInfo.ComplexProperties.TryGetValue(paramName, out var complexProperty))
            return complexProperty;

        // Try case-insensitive matches
        return entityInfo.SimpleProperties
            .FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase)).Value
            ?? entityInfo.ComplexProperties
               .FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static object? ConvertToParameterType(object? value, Type targetType)
    {
        // Delegate all conversions to SerializationBridge for consistency
        return SerializationBridge.FromNeo4jValue(value, targetType);
    }

    private static bool IsPathSegmentType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);

    private static object? GetDefaultValue(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;

    private static T ConvertToCollectionType<T>(List<object> items, Type elementType)
    {
        var targetType = typeof(T);

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }
            return (T)(object)array;
        }

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
                foreach (var item in items)
                {
                    list.Add(item);
                }
                return (T)list;
            }
        }

        throw new NotSupportedException($"Cannot convert results to collection type {targetType}");
    }
}
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

namespace Cvoya.Graph.Model.Serialization;

using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared result materializer that converts EntityInfo objects to strongly-typed .NET objects.
/// This component is provider-agnostic and works with any graph database through the IValueConverter abstraction.
/// Supports collections, path segments, projections, and complex object construction.
/// </summary>
/// <typeparam name="TValueConverter">Provider-specific value converter implementing IValueConverter</typeparam>
public sealed class ResultMaterializer<TValueConverter>
    where TValueConverter : IValueConverter
{
    private readonly EntityFactory _entityFactory;
    private readonly TValueConverter _valueConverter;
    private readonly ILogger<ResultMaterializer<TValueConverter>> _logger;

    /// <summary>
    /// Initializes a new instance of the ResultMaterializer with the specified dependencies.
    /// </summary>
    /// <param name="entityFactory">Factory for deserializing EntityInfo objects to .NET objects</param>
    /// <param name="valueConverter">Provider-specific value converter for handling database-specific types</param>
    /// <param name="loggerFactory">Optional logger factory for debugging and diagnostics</param>
    public ResultMaterializer(EntityFactory entityFactory, TValueConverter valueConverter, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        _logger = loggerFactory?.CreateLogger<ResultMaterializer<TValueConverter>>() ?? NullLogger<ResultMaterializer<TValueConverter>>.Instance;
    }

    /// <summary>
    /// Materializes a list of EntityInfo objects into the target type T.
    /// Handles both single objects and collections automatically based on the target type.
    /// </summary>
    /// <typeparam name="T">Target type for materialization</typeparam>
    /// <param name="entityInfos">List of EntityInfo objects to materialize</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Materialized object(s) of type T</returns>
    public async Task<T?> MaterializeAsync<T>(
        List<EntityInfo> entityInfos,
        CancellationToken cancellationToken = default)
    {
        var targetType = typeof(T);
        var elementType = CollectionTypeHelper.GetElementTypeOrSelf(targetType);
        var isCollectionType = CollectionTypeHelper.IsCollectionType(targetType);

        _logger.LogDebug("MaterializeAsync: TargetType={TargetType}, ElementType={ElementType}, IsCollectionType={IsCollectionType}, EntityInfoCount={EntityInfoCount}",
            targetType.Name, elementType.Name, isCollectionType, entityInfos.Count);

        // Handle empty results consistently
        if (!entityInfos.Any())
        {
            return isCollectionType
                ? CollectionTypeHelper.ConvertToCollectionType<T>([], elementType)
                : default;
        }

        var result = isCollectionType
            ? MaterializeAsCollection<T>(entityInfos, elementType)
            : MaterializeSingleElement<T>(entityInfos.FirstOrDefault(), elementType);

        _logger.LogDebug("MaterializeAsync: Final result type={ResultType}", result?.GetType().Name ?? "null");
        if (result is IEnumerable enumerable and not string)
        {
            var count = enumerable.Cast<object>().Count();
            _logger.LogDebug("MaterializeAsync: Collection result contains {Count} items", count);
        }

        await Task.CompletedTask; // Satisfy async signature for future extensibility
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

        var result = CollectionTypeHelper.ConvertToCollectionType<T>(elements!, elementType);
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

        if (result is null)
            return default(TResult);

        // Handle type conversions for primitive types and common value types like decimal
        if ((typeof(TResult).IsPrimitive || typeof(TResult) == typeof(decimal)) && result.GetType() != typeof(TResult))
        {
            try
            {
                // Special handling for decimal conversions from double
                if (typeof(TResult) == typeof(decimal) && result is double doubleValue)
                {
                    return (TResult)(object)Convert.ToDecimal(doubleValue);
                }
                return (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch
            {
                // Fall back to direct cast if conversion fails
                return (TResult)result;
            }
        }

        return (TResult)result;
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
        
        // We need to determine the concrete GraphPathSegment type to use
        // This requires provider-specific path segment implementations
        // For now, we'll use a generic approach that works with any provider
        var concreteType = FindConcretePathSegmentType(genericArgs)
            ?? throw new InvalidOperationException($"No concrete GraphPathSegment implementation found for {interfaceType.Name}");

        // Extract the three components from the path segment EntityInfo
        var startNodeEntityInfo = GetComplexProperty(entityInfo, "StartNode");
        var relationshipEntityInfo = GetComplexProperty(entityInfo, "Relationship");
        var endNodeEntityInfo = GetComplexProperty(entityInfo, "EndNode");

        // Materialize each component
        var startNode = MaterializeSingleElement<object>(startNodeEntityInfo, genericArgs[0]);
        var relationship = MaterializeSingleElement<object>(relationshipEntityInfo, genericArgs[1]);
        var endNode = MaterializeSingleElement<object>(endNodeEntityInfo, genericArgs[2]);

        // Find and invoke the constructor (works for both classes and records)
        var constructors = concreteType.GetConstructors()
            .Where(c => c.GetParameters().Length == 3)
            .ToArray();
            
        if (constructors.Length == 0)
        {
            throw new InvalidOperationException($"No 3-parameter constructor found for {concreteType.Name}");
        }

        var constructor = constructors[0];
        return constructor.Invoke([startNode, relationship, endNode]);
    }

    private static Type? FindConcretePathSegmentType(Type[] genericArgs)
    {
        // Look for a concrete GraphPathSegment implementation in loaded assemblies
        // This allows each provider to have their own implementation
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(type => 
                type.Name.StartsWith("GraphPathSegment") &&
                type.IsGenericTypeDefinition &&
                type.GetGenericArguments().Length == 3)?
            .MakeGenericType(genericArgs);
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
            return ConvertPropertyToParameterType(singleProperty, targetType);
        }

        return GetDefaultValue(targetType);
    }

    private object? CreateComplexObject(EntityInfo entityInfo, Type targetType)
    {
        // Use the actual type from EntityInfo instead of the target type
        // This allows interfaces like IRelationship to be materialized as their concrete types (e.g., Knows, Friend)
        var typeToInstantiate = entityInfo.ActualType ?? targetType;

        _logger.LogDebug("CreateComplexObject: Creating {TypeName} from EntityInfo with {SimpleCount} simple properties, {ComplexCount} complex properties",
            typeToInstantiate.Name, entityInfo.SimpleProperties.Count, entityInfo.ComplexProperties.Count);

        // Special handling for anonymous types - they have a predictable constructor pattern
        if (IsAnonymousType(typeToInstantiate))
        {
            return CreateAnonymousTypeObject(entityInfo, typeToInstantiate);
        }

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
                        SimpleValue => ConvertPropertyToParameterType(matchingProperty, param.ParameterType),
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

    private static bool IsAnonymousType(Type type) =>
        type.Name.StartsWith("<>f__AnonymousType") || 
        (type.Name.StartsWith("<>") && type.Name.Contains("AnonymousType"));

    private object? CreateAnonymousTypeObject(EntityInfo entityInfo, Type anonymousType)
    {
        _logger.LogDebug("CreateAnonymousTypeObject: Creating anonymous type {TypeName}", anonymousType.Name);

        // Anonymous types have a single constructor that takes all properties in order
        var constructor = anonymousType.GetConstructors().FirstOrDefault();
        if (constructor == null)
        {
            throw new InvalidOperationException($"Anonymous type {anonymousType.Name} has no constructor");
        }

        var parameters = constructor.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? $"param{i}";
            var matchingProperty = FindPropertyInEntityInfo(entityInfo, paramName);

            _logger.LogDebug("CreateAnonymousTypeObject: Parameter {ParamName} ({ParamType}) -> {Found}",
                paramName, param.ParameterType.Name, matchingProperty != null ? "FOUND" : "NOT_FOUND");

            if (matchingProperty?.Value is SimpleValue)
            {
                // Convert the value to the correct parameter type using the target type
                var convertedValue = ConvertPropertyToParameterType(matchingProperty, param.ParameterType);
                values[i] = convertedValue;
            }
            else if (matchingProperty?.Value is EntityInfo complexEntityInfo)
            {
                values[i] = MaterializeSingleElement<object>(complexEntityInfo, param.ParameterType);
            }
            else
            {
                values[i] = GetDefaultValue(param.ParameterType);
            }

            _logger.LogDebug("CreateAnonymousTypeObject: Parameter {ParamName} set to: {Value} (Type: {ValueType})",
                paramName, values[i]?.ToString() ?? "null", values[i]?.GetType().Name ?? "null");
        }

        var result = constructor.Invoke(values);
        _logger.LogDebug("CreateAnonymousTypeObject: Successfully created anonymous type instance");
        return result;
    }

    private object? ConvertPropertyToParameterType(Property property, Type parameterType)
    {
        // Use reflection to call the generic ConvertValue method with the correct type
        var method = typeof(IValueConverter).GetMethod(nameof(IValueConverter.ConvertValue))!;
        var genericMethod = method.MakeGenericMethod(parameterType);
        return genericMethod.Invoke(_valueConverter, [property]);
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

    private static bool IsPathSegmentType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);

    private static object? GetDefaultValue(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;
}
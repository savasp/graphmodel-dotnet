// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.Results;

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>Materializes provider-neutral graph records into requested CLR result shapes.</summary>
public sealed class GraphResultMaterializer
{
    private readonly EntityFactory _entityFactory;
    private readonly GraphResultProcessor _resultProcessor;
    private readonly ILogger<GraphResultMaterializer> _logger;

    /// <summary>Initializes a shared result materializer.</summary>
    public GraphResultMaterializer(EntityFactory entityFactory, ILoggerFactory? loggerFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<GraphResultMaterializer>() ?? NullLogger<GraphResultMaterializer>.Instance;
        _resultProcessor = new GraphResultProcessor(_entityFactory, loggerFactory);
    }

    /// <summary>Materializes a buffered result set.</summary>
    public async Task<T?> MaterializeAsync<T>(
        IReadOnlyList<GraphRecord> records,
        (Type Source, Type Relationship, Type Target)? graphPathTypes = null,
        CancellationToken cancellationToken = default)
    {
        var targetType = typeof(T);
        var elementType = GraphResultTypeHelpers.GetTargetTypeIfCollection(targetType);
        var isCollectionType = targetType != elementType;

        _logger.LogDebugGraphResultMaterializer38(targetType.Name, elementType.Name, isCollectionType, records.Count);

        // Handle empty results consistently
        if (!records.Any())
        {
            return isCollectionType
                ? ConvertToCollectionType<T>([], elementType)
                : default;
        }

        // TraversePaths decomposes a variable-length path into one row per hop; group those rows
        // back into ordered IGraphPath instances instead of going through the single-element
        // EntityInfo pipeline below (which has no notion of "many rows make up one result").
        if (graphPathTypes is { } types && elementType == typeof(IGraphPath))
        {
            var paths = MaterializeGraphPaths(records, types);
            return isCollectionType
                ? ConvertToCollectionType<T>([.. paths], elementType)
                : (T?)paths.FirstOrDefault();
        }

        // Process records to EntityInfo objects
        var entityInfos = await _resultProcessor.ProcessAsync(
            records,
            elementType,
            cancellationToken).ConfigureAwait(false);
        _logger.LogDebugGraphResultMaterializer65(entityInfos.Count);

        var result = isCollectionType
            ? MaterializeAsCollection<T>(entityInfos, elementType)
            : MaterializeSingleElement<T>(entityInfos.FirstOrDefault(), elementType);

        _logger.LogDebugGraphResultMaterializer71(result?.GetType().Name ?? "null");
        if (result is IEnumerable enumerable && !(result is string))
        {
            var count = enumerable.Cast<object>().Count();
            _logger.LogDebugGraphResultMaterializer75(count);
        }

        return result;
    }

    /// <summary>Materializes one result record for streaming execution.</summary>
    public async Task<T?> MaterializeRecordAsync<T>(
        GraphRecord record,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetType = typeof(T);
        var entityInfos = await _resultProcessor.ProcessAsync(
            [record],
            targetType,
            cancellationToken).ConfigureAwait(false);
        _logger.LogDebugGraphResultMaterializer93(entityInfos.Count);

        return MaterializeSingleElement<T>(entityInfos.FirstOrDefault(), targetType);
    }

    /// <summary>Processes one decomposed graph-path hop.</summary>
    public GraphResultProcessor.GraphPathHop ProcessGraphPathHop(
        GraphRecord record,
        (Type Source, Type Relationship, Type Target) graphPathTypes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _resultProcessor.ProcessGraphPathHop(
            record,
            graphPathTypes.Source,
            graphPathTypes.Relationship,
            graphPathTypes.Target);
    }

    /// <summary>Materializes an ordered set of decomposed hops as one graph path.</summary>
    public IGraphPath MaterializeGraphPath(
        IReadOnlyList<GraphResultProcessor.GraphPathHop> orderedHops,
        (Type Source, Type Relationship, Type Target) graphPathTypes)
    {
        var segments = new List<IGraphPathSegment>(orderedHops.Count);
        foreach (var hop in orderedHops.OrderBy(h => h.HopIndex))
        {
            var startNode = (Graph.INode)MaterializeSingleElement<object>(hop.StartNode, graphPathTypes.Source)!;
            var relationship = (Graph.IRelationship)MaterializeSingleElement<object>(hop.Relationship, graphPathTypes.Relationship)!;
            var endNode = (Graph.INode)MaterializeSingleElement<object>(hop.EndNode, graphPathTypes.Target)!;
            segments.Add(new GraphPathHopSegment(startNode, relationship, endNode));
        }

        if (segments.Count == 0)
            throw new GraphException("Cannot materialize an empty graph path.");

        return new GraphPath(segments[0].StartNode, segments[^1].EndNode, segments);
    }

    private List<IGraphPath> MaterializeGraphPaths(IReadOnlyList<GraphRecord> records, (Type Source, Type Relationship, Type Target) types)
    {
        var hops = _resultProcessor.ProcessGraphPathHops(
            records,
            types.Source,
            types.Relationship,
            types.Target);

        var paths = new List<IGraphPath>();
        var orderedHopsByPath = hops
            .GroupBy(h => h.PathIndex)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(h => h.HopIndex).ToList());
        foreach (var orderedHops in orderedHopsByPath)
        {
            paths.Add(MaterializeGraphPath(orderedHops, types));
        }

        return paths;
    }

    private T MaterializeAsCollection<T>(List<EntityInfo> entityInfos, Type elementType)
    {
        _logger.LogDebugGraphResultMaterializer156(entityInfos.Count, elementType.Name);

        var elements = entityInfos
            .Select(entityInfo => MaterializeSingleElement<object>(entityInfo, elementType))
            .Where(item => item is not null)
            .ToList();

        _logger.LogDebugGraphResultMaterializer164(elements.Count);

        var result = ConvertToCollectionType<T>(elements!, elementType);
        _logger.LogDebugGraphResultMaterializer167(result?.GetType().Name ?? "null");

        return result;
    }

    private TResult? MaterializeSingleElement<TResult>(EntityInfo? entityInfo, Type elementType)
    {
        if (entityInfo is null)
            return default(TResult);

        // Check if we should use the actual type from EntityInfo instead of elementType
        var typeToDeserialize = entityInfo.ActualType ?? elementType;
        var canDeserialize = _entityFactory.CanDeserialize(typeToDeserialize);

        _logger.LogDebugGraphResultMaterializer181(elementType.Name, entityInfo.ActualType?.Name ?? "null", canDeserialize);

        var result = canDeserialize
            ? _entityFactory.Deserialize(entityInfo)
            : CreateObjectFromEntityInfo(entityInfo, elementType);

        return result is null ? default(TResult) : (TResult)result;
    }

    private object? CreateObjectFromEntityInfo(EntityInfo entityInfo, Type targetType)
    {
        if (targetType == typeof(object) && entityInfo.SimpleProperties.Count == 1 && entityInfo.ComplexProperties.Count == 0)
        {
            return CreateSimpleValue(entityInfo, targetType);
        }

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

    private static EntityInfo? GetComplexProperty(EntityInfo entityInfo, string propertyName)
    {
        return entityInfo.ComplexProperties.TryGetValue(propertyName, out var property)
            && property.Value is EntityInfo nestedEntityInfo
            ? nestedEntityInfo
            : null;
    }

    private static object? CreateSimpleValue(EntityInfo entityInfo, Type targetType)
    {
        if (entityInfo.SimpleProperties.Count == 1)
        {
            var singleProperty = entityInfo.SimpleProperties.Values.First();
            if (singleProperty.Value is SimpleValue simpleValue)
            {
                if (simpleValue.Object is null && IsNonNullableValueType(targetType))
                {
                    throw NullValueException(targetType, parameterName: null);
                }

                return ConvertValueToTargetType(simpleValue.Object, targetType);
            }
        }

        return GetDefaultValue(targetType);
    }

    private static object? ConvertValueToTargetType(object? value, Type targetType)
    {
        // Convert provider-neutral scalar values to the requested CLR type.
        return GraphValueConverter.ConvertTo(value, targetType);
    }

    private object? CreateComplexObject(EntityInfo entityInfo, Type targetType)
    {
        // Use the actual type from EntityInfo instead of the target type
        // This allows interfaces like IRelationship to be materialized as their concrete types (e.g., Knows, Friend)
        var typeToInstantiate = entityInfo.ActualType ?? targetType;

        _logger.LogDebugGraphResultMaterializer278(typeToInstantiate.Name, entityInfo.SimpleProperties.Count, entityInfo.ComplexProperties.Count);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebugGraphResultMaterializer283(string.Join(", ", entityInfo.SimpleProperties.Keys));
            _logger.LogDebugGraphResultMaterializer285(string.Join(", ", entityInfo.ComplexProperties.Select(kvp => $"{kvp.Key}:{kvp.Value.Value?.GetType().Name ?? "null"}")));
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

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebugGraphResultMaterializer306(parameters.Length, string.Join(", ", parameters.Select(p => $"{p.Name}:{p.ParameterType.Name}")));
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name ?? $"param{i}";
                    var matchingProperty = FindPropertyInEntityInfo(entityInfo, paramName);

                    _logger.LogDebugGraphResultMaterializer316(paramName, matchingProperty != null ? "FOUND" : "NOT_FOUND", matchingProperty?.Value?.GetType().Name ?? "null");

                    values[i] = matchingProperty?.Value switch
                    {
                        SimpleValue simpleValue => ConvertToParameterType(simpleValue.Object, param),
                        EntityInfo complexEntityInfo => MaterializeSingleElement<object>(complexEntityInfo, param.ParameterType),
                        EntityCollection entityCollection => MaterializeEntityCollection(entityCollection, param.ParameterType),
                        SimpleCollection simpleCollection => MaterializeSimpleCollection(simpleCollection, param),
                        _ => param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType)
                    };

                    _logger.LogDebugGraphResultMaterializer327(paramName, values[i]?.GetType().Name ?? "null");
                }

                // Use constructor.Invoke instead of Activator.CreateInstance to avoid ambiguity
                var result = constructor.Invoke(values);
                _logger.LogDebugGraphResultMaterializer335(typeToInstantiate.Name);
                return result;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is AmbiguousMatchException || ex is TargetParameterCountException)
            {
                _logger.LogDebugGraphResultMaterializer340(ex.Message);
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

    private static object? ConvertToParameterType(object? value, ParameterInfo parameter)
    {
        var targetType = parameter.ParameterType;

        if (value is null)
        {
            if (IsNonNullableValueType(targetType))
            {
                throw NullValueException(targetType, ParameterName(parameter));
            }

            return null;
        }

        // A projected list of scalars arrives as a wire list inside a SimpleValue rather than as a
        // SimpleCollection, so its elements are checked here rather than in MaterializeSimpleCollection.
        RejectNullElements(value, parameter, targetType);

        // Convert provider-neutral scalar values to the requested CLR type.
        return GraphValueConverter.ConvertTo(value, targetType);
    }

    private static void RejectNullElements(object value, ParameterInfo parameter, Type targetType)
    {
        if (value is not IEnumerable source || value is string)
        {
            return;
        }

        // Deliberately not GetTargetTypeIfCollection: that returns the type itself when it is not a
        // collection, so an `object` parameter would resolve `object` as its own element type and
        // reject the nulls a wire list is entitled to carry into it. Returning null here skips the
        // scan for any target that declares no element contract to enforce.
        var elementType = GetCollectionElementType(targetType);
        if (elementType is null)
        {
            return;
        }

        // A CLR collection of a non-nullable value type cannot contain null, so scanning it
        // (boxing every element — e.g. each byte of a byte[] blob) proves nothing. Resolve what the
        // source actually yields through its IEnumerable<T> interface: a generic type's first
        // argument is not necessarily its element type (List<long>.Select(...) is an iterator over
        // long that yields anything), and reading it as one would skip the scan over a real null.
        if (IsNonNullableValueType(GraphResultTypeHelpers.GetTargetTypeIfCollection(value.GetType())))
        {
            return;
        }

        // Nullable metadata is read on the first null only — a collection carrying none never needs it.
        var index = 0;
        foreach (var item in source)
        {
            if (item is null)
            {
                if (NullabilityDerivation.IsElementNullable(parameter, elementType))
                {
                    return;
                }

                throw NullElementException(parameter, elementType, index);
            }

            index++;
        }
    }

    private static Type? GetCollectionElementType(Type type) => type.IsArray
        ? type.GetElementType()
        : type.IsGenericType
            ? type.GetGenericArguments().FirstOrDefault()
            : null;

    private static bool IsNonNullableValueType(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) is null;

    private static string ParameterName(ParameterInfo parameter) =>
        parameter.Name ?? $"param{parameter.Position}";

    private static GraphException NullValueException(Type targetType, string? parameterName) =>
        new(parameterName is null
            ? $"Cannot materialize null into non-nullable type '{targetType.FullName}'."
            : $"Cannot materialize null into non-nullable type '{targetType.FullName}' for '{parameterName}'.");

    private static GraphException NullElementException(ParameterInfo parameter, Type elementType, int index) =>
        new($"Constructor parameter '{ParameterName(parameter)}' contains a null element at index {index}, " +
            $"but its target element type '{elementType}' is non-nullable.");

    private static bool IsPathSegmentType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);

    private static object? GetDefaultValue(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;

    private object? MaterializeEntityCollection(EntityCollection collection, Type targetType)
    {
        var elementType = GraphResultTypeHelpers.GetTargetTypeIfCollection(targetType);
        var elements = collection.Entities
            .Select(entity => MaterializeSingleElement<object>(entity, elementType))
            .ToList();
        return BuildCollection(elements, elementType, targetType);
    }

    // No wire shape currently binds a SimpleCollection to a constructor parameter — projections carry
    // scalar lists as a wire list inside a SimpleValue, which ConvertToParameterType checks instead.
    // The guard below keeps this path on the same contract as its two siblings for whenever one does;
    // whether the path is dead outright is #429.
    private static object? MaterializeSimpleCollection(SimpleCollection collection, ParameterInfo parameter)
    {
        var targetType = parameter.ParameterType;
        var elementType = GraphResultTypeHelpers.GetTargetTypeIfCollection(targetType);
        var elements = new List<object?>(collection.Values.Count);

        bool? isElementNullable = null;
        var index = 0;
        foreach (var value in collection.Values)
        {
            if (value.Object is null)
            {
                isElementNullable ??= NullabilityDerivation.IsElementNullable(parameter, elementType);
                if (isElementNullable is false)
                {
                    throw NullElementException(parameter, elementType, index);
                }
            }

            elements.Add(GraphValueConverter.ConvertTo(value.Object, elementType));
            index++;
        }

        return BuildCollection(elements, elementType, targetType);
    }

    private static object BuildCollection(List<object?> items, Type elementType, Type targetType)
    {
        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                array.SetValue(items[index], index);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

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
            return Cast<T>(array);
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

    private static T Cast<T>(object value)
    {
        return (T)value;
    }
}

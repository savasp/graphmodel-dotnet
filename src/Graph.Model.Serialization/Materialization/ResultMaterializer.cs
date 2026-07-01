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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared result materializer that converts EntityInfo objects to strongly-typed .NET objects.
/// </summary>
public sealed class ResultMaterializer<TValueConverter>
    where TValueConverter : IValueConverter
{
    private readonly EntityFactory _entityFactory;
    private readonly TValueConverter _valueConverter;
    private readonly ILogger<ResultMaterializer<TValueConverter>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultMaterializer{TValueConverter}"/> class.
    /// </summary>
    /// <param name="entityFactory">The entity factory for deserializing EntityInfo objects.</param>
    /// <param name="valueConverter">The provider-specific value converter.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
    public ResultMaterializer(EntityFactory entityFactory, TValueConverter valueConverter, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        _logger = loggerFactory?.CreateLogger<ResultMaterializer<TValueConverter>>() ?? NullLogger<ResultMaterializer<TValueConverter>>.Instance;
    }

    /// <summary>
    /// Materializes a list of EntityInfo objects into a strongly-typed result.
    /// </summary>
    /// <typeparam name="T">The target type to materialize to.</typeparam>
    /// <param name="entityInfos">The list of EntityInfo objects to materialize.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The materialized result, or default if no entities are provided.</returns>
    public async Task<T?> MaterializeAsync<T>(List<EntityInfo> entityInfos, CancellationToken cancellationToken = default)
    {
        var targetType = typeof(T);
        var elementType = CollectionTypeHelper.GetElementTypeOrSelf(targetType);
        var isCollectionType = CollectionTypeHelper.IsCollectionType(targetType);

        if (!entityInfos.Any())
        {
            return isCollectionType
                ? CollectionTypeHelper.ConvertToCollectionType<T>([], elementType)
                : default;
        }

        var result = isCollectionType
            ? MaterializeAsCollection<T>(entityInfos, elementType)
            : MaterializeSingleElement<T>(entityInfos.First(), elementType);

        await Task.CompletedTask;
        return result;
    }

    private T MaterializeAsCollection<T>(List<EntityInfo> entityInfos, Type elementType)
    {
        var elements = entityInfos
            .Select(entityInfo => MaterializeSingleElement<object>(entityInfo, elementType))
            .Where(item => item is not null)
            .ToList();
        return CollectionTypeHelper.ConvertToCollectionType<T>(elements!, elementType);
    }

    private TResult? MaterializeSingleElement<TResult>(EntityInfo entityInfo, Type elementType)
    {
        var typeToDeserialize = entityInfo.ActualType ?? elementType;
        var canDeserialize = _entityFactory.CanDeserialize(typeToDeserialize);

        var result = canDeserialize
            ? _entityFactory.Deserialize(entityInfo)
            : CreateObjectFromEntityInfo(entityInfo, elementType);

        if (result is null) return default;

        if ((typeof(TResult).IsPrimitive || typeof(TResult) == typeof(decimal)) && result.GetType() != typeof(TResult))
        {
            try
            {
                if (typeof(TResult) == typeof(decimal) && result is double doubleValue)
                    return (TResult)(object)Convert.ToDecimal(doubleValue);
                return (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch { return (TResult)result; }
        }

        return (TResult)result;
    }

    private object? CreateObjectFromEntityInfo(EntityInfo entityInfo, Type targetType)
    {
        if (IsPathSegmentType(targetType))
            return CreatePathSegmentFromEntityInfo(entityInfo, targetType);
        if (GraphDataModel.IsSimple(targetType))
            return CreateSimpleValue(entityInfo, targetType);
        return CreateComplexObject(entityInfo, targetType);
    }

    private object CreatePathSegmentFromEntityInfo(EntityInfo entityInfo, Type interfaceType)
    {
        var genericArgs = interfaceType.GetGenericArguments();
        var concreteType = FindConcretePathSegmentType(genericArgs)
            ?? throw new InvalidOperationException($"No concrete GraphPathSegment found for {interfaceType.Name}");
        var startNodeEntityInfo = GetComplexProperty(entityInfo, "StartNode");
        var relationshipEntityInfo = GetComplexProperty(entityInfo, "Relationship");
        var endNodeEntityInfo = GetComplexProperty(entityInfo, "EndNode");
        var startNode = MaterializeSingleElement<object>(startNodeEntityInfo!, genericArgs[0]);
        var relationship = MaterializeSingleElement<object>(relationshipEntityInfo!, genericArgs[1]);
        var endNode = MaterializeSingleElement<object>(endNodeEntityInfo!, genericArgs[2]);
        var constructor = concreteType.GetConstructors().First(c => c.GetParameters().Length == 3);
        return constructor.Invoke([startNode, relationship, endNode]);
    }

    private static Type? FindConcretePathSegmentType(Type[] genericArgs)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name.StartsWith("GraphPathSegment") && t.IsGenericTypeDefinition && t.GetGenericArguments().Length == 3)?
            .MakeGenericType(genericArgs);
    }

    private static EntityInfo? GetComplexProperty(EntityInfo entityInfo, string propertyName)
    {
        return entityInfo.ComplexProperties.TryGetValue(propertyName, out var property)
            && property.Value is EntityInfo nestedEntityInfo ? nestedEntityInfo : null;
    }

    private object? CreateSimpleValue(EntityInfo entityInfo, Type targetType)
    {
        if (entityInfo.SimpleProperties.Count == 1)
            return ConvertPropertyToParameterType(entityInfo.SimpleProperties.First().Value, targetType);
        return GetDefaultValue(targetType);
    }

    private object? CreateComplexObject(EntityInfo entityInfo, Type targetType)
    {
        var typeToInstantiate = entityInfo.ActualType ?? targetType;

        if (typeof(INode).IsAssignableFrom(targetType) || typeof(IRelationship).IsAssignableFrom(targetType))
            return _entityFactory.Deserialize(entityInfo);

        if (IsAnonymousType(typeToInstantiate))
            return CreateAnonymousTypeObject(entityInfo, typeToInstantiate);

        var constructors = typeToInstantiate.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length).ToList();

        foreach (var constructor in constructors)
        {
            try
            {
                var parameters = constructor.GetParameters();
                var values = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = param.Name ?? $"param{i}";
                    var matchingProperty = FindPropertyInEntityInfo(entityInfo, paramName);
                    values[i] = matchingProperty?.Value switch
                    {
                        SimpleValue simpleVal when param.ParameterType.IsGenericType &&
                            param.ParameterType.GetGenericTypeDefinition() == typeof(List<>) =>
                            MaterializeListProperty(simpleVal, param.ParameterType),
                        SimpleValue => ConvertPropertyToParameterType(matchingProperty, param.ParameterType),
                        EntityInfo complexEntityInfo => MaterializeSingleElement<object>(complexEntityInfo, param.ParameterType),
                        _ => param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType)
                    };
                }
                return constructor.Invoke(values);
            }
            catch when (constructor != constructors.Last()) { continue; }
        }

        throw new InvalidOperationException($"Could not find a suitable constructor for type {typeToInstantiate.Name}");
    }

    private static bool IsAnonymousType(Type type) =>
        type.Name.StartsWith("<>f__AnonymousType") || (type.Name.StartsWith("<>") && type.Name.Contains("AnonymousType"));

    private object? CreateAnonymousTypeObject(EntityInfo entityInfo, Type anonymousType)
    {
        var constructor = anonymousType.GetConstructors().FirstOrDefault()
            ?? throw new InvalidOperationException($"Anonymous type {anonymousType.Name} has no constructor");
        var parameters = constructor.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var paramName = param.Name ?? $"param{i}";
            var matchingProperty = FindPropertyInEntityInfo(entityInfo, paramName);

            if (matchingProperty?.Value is SimpleValue simpleVal)
            {
                // Check for List<T> properties (from collect() results)
                if (param.ParameterType.IsGenericType &&
                    param.ParameterType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    values[i] = MaterializeListProperty(simpleVal, param.ParameterType);
                }
                else
                {
                    values[i] = ConvertPropertyToParameterType(matchingProperty, param.ParameterType);
                }
            }
            else if (matchingProperty?.Value is EntityInfo complexEntityInfo)
                values[i] = typeof(INode).IsAssignableFrom(param.ParameterType)
                    ? _entityFactory.Deserialize(complexEntityInfo)
                    : MaterializeSingleElement<object>(complexEntityInfo, param.ParameterType);
            else
                values[i] = GetDefaultValue(param.ParameterType);
        }

        return constructor.Invoke(values);
    }

    /// <summary>
    /// Materializes a List{T} property from a SimpleValue that contains a raw list
    /// of values (produced by collect() in AGE queries).
    /// Each element may be an EntityInfo (for map projections) or a scalar value.
    /// </summary>
    private object? MaterializeListProperty(SimpleValue simpleValue, Type listType)
    {
        var elementType = listType.GetGenericArguments()[0];

        if (simpleValue.Object is not System.Collections.IList rawList)
            return GetDefaultValue(listType);

        if (rawList.Count == 0)
        {
            var emptyListType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(emptyListType);
        }

        var typedListType = typeof(List<>).MakeGenericType(elementType);
        var typedList = (System.Collections.IList)Activator.CreateInstance(typedListType)!;

        foreach (var item in rawList)
        {
            if (item == null)
            {
                typedList.Add(null);
            }
            else if (item is EntityInfo ei)
            {
                // Map projection: collect({Name: ..., Age: ...}) produces EntityInfo elements
                var materialized = MaterializeSingleElement<object>(ei, elementType);
                typedList.Add(materialized);
            }
            else
            {
                // Scalar projection: collect(tgt0.FirstName) produces typed scalar values
                try
                {
                    var converted = Convert.ChangeType(item, elementType);
                    typedList.Add(converted);
                }
                catch
                {
                    typedList.Add(item);
                }
            }
        }

        return typedList;
    }

    private object? ConvertPropertyToParameterType(Property property, Type parameterType)
    {
        var method = typeof(IValueConverter).GetMethod(nameof(IValueConverter.ConvertValue))!;
        var genericMethod = method.MakeGenericMethod(parameterType);
        return genericMethod.Invoke(_valueConverter, [property]);
    }

    private static Property? FindPropertyInEntityInfo(EntityInfo entityInfo, string paramName)
    {
        if (entityInfo.SimpleProperties.TryGetValue(paramName, out var simpleProp)) return simpleProp;
        if (entityInfo.ComplexProperties.TryGetValue(paramName, out var complexProp)) return complexProp;
        return entityInfo.SimpleProperties.FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase)).Value
            ?? entityInfo.ComplexProperties.FirstOrDefault(kv => string.Equals(kv.Key, paramName, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool IsPathSegmentType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IGraphPathSegment<,,>);

    private static object? GetDefaultValue(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;
}

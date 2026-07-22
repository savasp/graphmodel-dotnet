// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Builders;

using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


/// <summary>
/// Builds parameters for Cypher queries from entities and values.
/// </summary>
internal sealed class CypherParameterBuilder
{
    private readonly EntityFactory _entityFactory;
    private readonly ILogger<CypherParameterBuilder> _logger;

    public CypherParameterBuilder(EntityFactory entityFactory, ILoggerFactory? loggerFactory = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _logger = loggerFactory?.CreateLogger<CypherParameterBuilder>() ?? NullLogger<CypherParameterBuilder>.Instance;
    }

    /// <summary>
    /// Builds parameters for creating or updating an entity.
    /// </summary>
    public Dictionary<string, object?> BuildEntityParameters(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var parameters = new Dictionary<string, object?>();

        if (entity.GetType().IsConstructedGenericType)
        {
            foreach (var (key, value) in SerializationBridge.CreateMetadata(entity.GetType()))
            {
                parameters[key] = value;
            }
        }

        // Use EntityFactory to serialize the entity
        var entityInfo = _entityFactory.Serialize(entity);

        // Add simple properties only - complex properties are handled as separate nodes.
        foreach (var (name, value) in SimpleCollectionStorageCodec.EncodeProperties(
            entityInfo.SimpleProperties,
            omitNullPayloads: false,
            SerializationBridge.ToAgeValue))
        {
            parameters[name] = value;
        }

        return parameters;
    }

    /// <summary>
    /// Builds a parameter value from any object.
    /// </summary>
    public object? BuildParameterValue(object? value)
    {
        if (value == null)
            return null;

        // For entities, return their properties
        return value switch
        {
            IEntity entity => BuildEntityParameters(entity),
            // For everything else, use the bridge
            _ => SerializationBridge.ToAgeValue(value),
        };
    }
}

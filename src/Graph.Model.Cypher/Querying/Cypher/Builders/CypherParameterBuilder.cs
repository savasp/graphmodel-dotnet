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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Serialization;
using Cvoya.Graph.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds parameters for Cypher queries from entities and values in a provider-agnostic way.
/// </summary>
public sealed class CypherParameterBuilder
{
    private readonly EntityFactory entityFactory;
    private readonly ICypherValueSerializer valueSerializer;
    private readonly ILogger<CypherParameterBuilder> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherParameterBuilder"/> class.
    /// </summary>
    /// <param name="entityFactory">The entity factory for serializing entities.</param>
    /// <param name="valueSerializer">The value serializer for converting CLR values.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
    public CypherParameterBuilder(
        EntityFactory entityFactory,
        ICypherValueSerializer valueSerializer,
        ILoggerFactory? loggerFactory = null)
    {
        this.entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        this.valueSerializer = valueSerializer ?? throw new ArgumentNullException(nameof(valueSerializer));
        logger = loggerFactory?.CreateLogger<CypherParameterBuilder>() ?? NullLogger<CypherParameterBuilder>.Instance;
    }

    /// <summary>
    /// Builds parameters for creating or updating an entity.
    /// </summary>
    public Dictionary<string, object?> BuildEntityParameters(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var type = entity.GetType();
        var parameters = new Dictionary<string, object?>();

        var metadata = valueSerializer.CreateMetadata(type);
        foreach (var (key, value) in metadata)
        {
            parameters[key] = value;
        }

        var entityInfo = entityFactory.Serialize(entity);

        foreach (var (name, property) in entityInfo.SimpleProperties)
        {
            switch (property.Value)
            {
                case SimpleValue simpleValue:
                    parameters[name] = valueSerializer.ConvertValue(simpleValue.Object);
                    break;
                case SimpleCollection simpleCollection:
                    parameters[name] = simpleCollection.Values
                        .Select(valueSerializer.ConvertValue)
                        .ToList();
                    break;
                default:
                    logger.LogDebug("Skipping unsupported property shape for {Property}", name);
                    break;
            }
        }

        return parameters;
    }

    /// <summary>
    /// Builds a parameter value from any object.
    /// </summary>
    public object? BuildParameterValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            IEntity entity => BuildEntityParameters(entity),
            _ => valueSerializer.ConvertValue(value),
        };
    }
}

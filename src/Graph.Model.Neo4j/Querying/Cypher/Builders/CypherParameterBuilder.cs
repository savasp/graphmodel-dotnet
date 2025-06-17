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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

using Cvoya.Graph.Model.Neo4j.Serialization;
using Cvoya.Graph.Model.Serialization;
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

        var type = entity.GetType();
        var parameters = new Dictionary<string, object?>();

        // Add metadata
        var metadata = SerializationBridge.CreateMetadata(type);
        foreach (var (key, value) in metadata)
        {
            parameters[key] = value;
        }

        // Use EntityFactory to serialize the entity
        var entityInfo = _entityFactory.Serialize(entity);

        // Add simple properties only - complex properties are handled as separate nodes
        foreach (var (name, property) in entityInfo.SimpleProperties)
        {
            if (property.Value is SimpleValue simpleValue)
            {
                parameters[name] = SerializationBridge.ToNeo4jValue(simpleValue.Object);
            }
            else if (property.Value is SimpleCollection simpleCollection)
            {
                var list = new List<object?>();
                foreach (var item in simpleCollection.Values)
                {
                    list.Add(SerializationBridge.ToNeo4jValue(item));
                }
                parameters[name] = list;
            }
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
            _ => SerializationBridge.ToNeo4jValue(value),
        };
    }
}
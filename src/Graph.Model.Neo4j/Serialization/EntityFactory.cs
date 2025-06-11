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

using System.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Default implementation of entity factory that uses reflection as a fallback
/// </summary>
internal class EntityFactory(ILoggerFactory? loggerFactory = null)
{
    private readonly ILogger<EntityFactory>? _logger = loggerFactory?.CreateLogger<EntityFactory>() ?? NullLogger<EntityFactory>.Instance;

    public object CreateInstance(Type type, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        // First check if we have a generated serializer that can handle creation
        var serializer = EntitySerializerRegistry.GetSerializer(type);
        if (serializer != null)
        {
            _logger?.LogDebug("No generated serializer found for type {Type}. Falling back to reflection-based creation.", type.Name);

            var serializedEntity = ConvertToIntermediateRepresentation(type, neo4jEntity);

            // The serializer will handle the entire deserialization process
            return serializer.Deserialize(serializedEntity);
        }

        _logger?.LogDebug("No generated serializer found for type {Type}. Falling back to reflection-based creation.", type.Name);
        // Fallback to reflection-based creation for types without generated serializers
        return CreateInstanceViaReflection(type, neo4jEntity);
    }

    private Entity ConvertToIntermediateRepresentation(Type type, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        var simpleProperties = new Dictionary<string, Property>();
        var complexProperties = new Dictionary<string, Property>();

        // TODO: Implement conversion logic for simple and complex properties
        return new Entity(
            Type: type,
            Label: string.Empty,
            SimpleProperties: simpleProperties,
            ComplexProperties: complexProperties
        );
    }

    private object CreateInstanceViaReflection(Type type, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        try
        {
            // Try parameterless constructor first
            var parameterlessConstructor = type.GetConstructor(Type.EmptyTypes);
            if (parameterlessConstructor != null)
            {
                _logger?.LogDebug("Creating instance of {Type} using parameterless constructor", type.Name);
                return Activator.CreateInstance(type)!;
            }

            // Look for constructors and try to match parameters with Neo4j properties
            var constructors = type.GetConstructors()
                .OrderBy(c => c.GetParameters().Length)
                .ToList();

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var paramValues = new object?[parameters.Length];
                var allParametersMatched = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var paramName = char.ToUpper(param.Name![0]) + param.Name[1..]; // Convert to PascalCase

                    if (neo4jEntity.Properties.TryGetValue(paramName, out var value))
                    {
                        // We'll need the value converter here
                        paramValues[i] = value; // TODO: Convert using ValueConverter
                    }
                    else if (param.HasDefaultValue)
                    {
                        paramValues[i] = param.DefaultValue;
                    }
                    else
                    {
                        allParametersMatched = false;
                        break;
                    }
                }

                if (allParametersMatched)
                {
                    _logger?.LogDebug("Creating instance of {Type} using constructor with {ParamCount} parameters",
                        type.Name, parameters.Length);
                    return constructor.Invoke(paramValues);
                }
            }

            throw new InvalidOperationException($"No suitable constructor found for type {type.Name}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create instance of type {Type}", type.Name);
            throw new GraphException($"Failed to create instance of type {type.Name}", ex);
        }
    }
}
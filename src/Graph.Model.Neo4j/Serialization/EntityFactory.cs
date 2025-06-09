using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Default implementation of entity factory that uses reflection as a fallback
/// </summary>
internal class EntityFactory(ILogger<EntityFactory>? logger = null)
{
    private readonly ILogger<EntityFactory>? _logger = logger;

    public object CreateInstance(Type type, global::Neo4j.Driver.IEntity neo4jEntity)
    {
        // First check if we have a generated serializer that can handle creation
        var serializer = EntitySerializerRegistry.GetSerializer(type);
        if (serializer != null)
        {
            // The serializer will handle the entire deserialization process
            return serializer.DeserializeAsync(neo4jEntity).GetAwaiter().GetResult();
        }

        // Fallback to reflection-based creation for types without generated serializers
        return CreateInstanceViaReflection(type, neo4jEntity);
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
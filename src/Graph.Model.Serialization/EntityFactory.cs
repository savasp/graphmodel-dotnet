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
/// Represents the serialization/deserialization logic for the graph model.
/// </summary>
/// <param name="loggerFactory">Optional logger factory for logging.</param>
public class EntityFactory(ILoggerFactory? loggerFactory = null)
{
    private readonly ILogger<EntityFactory> _logger = loggerFactory?.CreateLogger<EntityFactory>()
        ?? NullLogger<EntityFactory>.Instance;

    private readonly EntitySerializerRegistry _serializerRegistry = new();

    /// <summary>
    /// Deserializes an <see cref="IEntity"/>" from its serialized form.
    /// </summary>
    /// <param name="entity">The serialized entity information.</param>
    /// <returns>A .NET object graph</returns>
    public object Deserialize(EntityInfo entity)
    {
        var serializer = _serializerRegistry.GetSerializer(entity.ActualType)
            ?? throw new GraphException($"No serializer found for type {entity.ActualType}. Ensure it is registered in the EntitySerializerRegistry.");

        return serializer.Deserialize(entity);
    }

    /// <summary>
    /// Deserializes an <see cref="EntityInfo"/> into a .NET object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity to deserialize into, which must implement <see cref="IEntity"/>.</typeparam>
    /// <param name="entity">The <see cref="EntityInfo"/> to deserialize.</param>
    /// <returns>A .NET object graph from the <see cref="EntityInfo"/> representation.</returns>
    public T Deserialize<T>(EntityInfo entity) => (T)Deserialize(entity);

    /// <summary>
    /// Serializes an <see cref="IEntity"/> into an <see cref="EntityInfo"/> representation.
    /// </summary>
    /// <param name="entity">The .NET object to serialize, which must implement <see cref="IEntity"/>.</param>
    /// <returns>An <see cref="EntityInfo"/> representation of the .NET object.</returns>
    public EntityInfo Serialize(IEntity entity)
    {
        var serializer = _serializerRegistry.GetSerializer(entity.GetType())
            ?? throw new GraphException($"No serializer found for type {entity.GetType().Name}. Ensure it is registered in the EntitySerializerRegistry.");

        return serializer.Serialize(entity);
    }

    /// <summary>
    /// Checks if the factory can deserialize a given type.
    /// </summary>
    /// <param name="type">The type to check for deserialization capability.</param>
    /// <returns>True if the factory can deserialize the type, otherwise false.</returns>
    public bool CanDeserialize(Type type) =>
        _serializerRegistry.ContainsType(type) ||
        typeof(INode).IsAssignableFrom(type) ||
        typeof(IRelationship).IsAssignableFrom(type);

    /// <summary>
    /// Retrieves the schema for a given entity type.
    /// </summary>
    /// <param name="entityType">The type of the entity for which to retrieve the schema.</param>
    /// <returns>An <see cref="EntitySchema"/> representing the schema of the entity type, or null if no serializer is found.</returns>
    /// <exception cref="GraphException"></exception>
    public EntitySchema? GetSchema(Type entityType)
    {
        var serializer = _serializerRegistry.GetSerializer(entityType)
            ?? null;

        return serializer?.GetSchema();
    }
}
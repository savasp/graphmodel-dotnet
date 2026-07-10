// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Base class for generated entity serializers
/// </summary>
public interface IEntitySerializer
{
    /// <summary>
    /// Gets the type of the entity this serializer handles
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Serializes a .NET object into an <see cref="EntityInfo"/> representation
    /// </summary>
    /// <param name="obj">The .NET object to serialize</param>
    /// <returns>An <see cref="EntityInfo"/> representation of the .NET object</returns>
    EntityInfo Serialize(object obj);

    /// <summary>
    /// Deserializes an <see cref="EntityInfo"/> into a .NET object
    /// </summary>
    /// <param name="entity">The <see cref="EntityInfo"/> to deserialize</param>
    /// <returns>A .NET object graph from the <see cref="EntityInfo"/> representation</returns>
    object Deserialize(EntityInfo entity);

    /// <summary>
    /// Gets the schema information for the entity type this serializer handles
    /// </summary>
    /// <returns>A <see cref="EntitySchema"/> object representing the schema</returns>
    EntitySchema GetSchema();
}
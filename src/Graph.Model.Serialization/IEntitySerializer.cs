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
    /// <param name="entity">The .NET object to serialize, which must implement <see cref="IEntity"/></param>
    /// <returns>An <see cref="EntityInfo"/> representation of the .NET object</returns>
    EntityInfo Serialize(IEntity entity);

    /// <summary>
    /// Deserializes an <see cref="EntityInfo"/> into a .NET object
    /// </summary>
    /// <param name="entity">The <see cref="EntityInfo"/> to deserialize</param>
    /// <returns>A .NET object graph from the <see cref="EntityInfo"/> representation</returns>
    IEntity Deserialize(EntityInfo entity);

    /// <summary>
    /// Deserializes an <see cref="EntityInfo"/> into an <see cref="IEntity"/> 
    /// </summary>
    /// <param name="entity">The <see cref="EntityInfo"/> to deserialize</param>
    /// <typeparam name="T">The type of the entity to deserialize into, which must implement <see cref="IEntity"/>.</typeparam>
    /// <returns>A .NET object graph from the <see cref="EntityInfo"/> representation</returns>
    T Deserialize<T>(EntityInfo entity) where T : IEntity;

    /// <summary>
    /// Gets the schema information for the entity type this serializer handles
    /// </summary>
    /// <returns>A <see cref="EntitySchema"/> object representing the schema</returns>
    EntitySchema GetSchema();
}